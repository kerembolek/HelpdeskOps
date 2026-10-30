using System.Net;
using System.Security.Cryptography;
using System.Text;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HelpdeskOps.Services.Email;

// Turns one mail from the helpdesk mailbox into a ticket, or into a comment when it answers one of our mails.
// Only people with an open account are served, and a sender gets no more rights than on the site: the address in the
// From header can be forged, so a mail can only do what is harmless, and all answers go to the account's own address,
// never to a Reply-To.
public class InboundEmailProcessor
{
    // Enough for anyone by hand; stops a mail loop or a stolen mailbox from filling the ticket list
    public const int MaxMailsPerHour = 20;

    // Below this an inline picture is a logo or signature, not a screenshot
    private const long MinInlineImageSize = 20 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly AttachmentStorage _storage;
    private readonly EmailQueue _queue;
    private readonly EmailOptions _options;

    public InboundEmailProcessor(ApplicationDbContext context, AttachmentStorage storage, EmailQueue queue, IOptions<EmailOptions> options)
    {
        _context = context;
        _storage = storage;
        _queue = queue;
        _options = options.Value;
    }

    public async Task<InboundEmailResult> ProcessAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        var hash = Hash(message);
        if (await _context.InboundEmails.AnyAsync(e => e.MessageHash == hash, cancellationToken))
        {
            return InboundEmailResult.Skipped;
        }

        var skip = InboundEmailRules.SkipReason(message, _options.Inbound.UserName, _options.FromAddress);
        var user = skip == null ? await FindSenderAsync(message, cancellationToken) : null;
        skip ??= user == null ? "Gönderen açık bir hesaba ait değil." : null;

        if (skip == null && await _context.InboundEmails.CountAsync(
                e => e.UserId == user!.Id && e.ReceivedAt > DateTime.UtcNow.AddHours(-1), cancellationToken) >= MaxMailsPerHour)
        {
            skip = "Saatlik e-posta sınırı aşıldı.";
        }
        if (skip != null)
        {
            return await RecordAsync(hash, user?.Id, null, InboundEmailResult.Skipped, skip, cancellationToken);
        }

        var ticket = await FindAnsweredTicketAsync(message, user!, cancellationToken);
        return ticket != null
            ? await AddCommentAsync(message, hash, user!, ticket, cancellationToken)
            : await OpenTicketAsync(message, hash, user!, cancellationToken);
    }

    private async Task<ApplicationUser?> FindSenderAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var address = message.From.Mailboxes.Single().Address.ToUpperInvariant();
        return await _context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == address && u.AnonymizedAt == null
            && (u.LockoutEnd == null || u.LockoutEnd != DateTimeOffset.MaxValue), cancellationToken);
    }

    // Same rule as the site: the requester and IT staff can write on a ticket. Anyone else opens a ticket of their own.
    private async Task<Ticket?> FindAnsweredTicketAsync(MimeMessage message, ApplicationUser user, CancellationToken cancellationToken)
    {
        if (InboundEmailRules.TicketIdFromSubject(message.Subject) is not int id
            || await _context.Tickets.FindAsync(new object[] { id }, cancellationToken) is not Ticket ticket)
        {
            return null;
        }

        var isItStaff = await (
            from userRole in _context.UserRoles
            join role in _context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id && role.Name == AppRoles.ITAdmin
            select userRole).AnyAsync(cancellationToken);
        return ticket.RequesterId == user.Id || isItStaff ? ticket : null;
    }

    private async Task<InboundEmailResult> OpenTicketAsync(MimeMessage message, string hash, ApplicationUser user, CancellationToken cancellationToken)
    {
        var body = InboundEmailRules.Body(message);
        var ticket = new Ticket
        {
            Title = InboundEmailRules.Title(message.Subject),
            Description = body.Length == 0 ? "(E-postada metin yoktu.)" : InboundEmailRules.Shorten(body, InboundEmailRules.MaxDescriptionLength),
            // IT sorts these out; the sender has no form to choose them on
            Category = TicketCategory.Other,
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            RequesterId = user.Id
        };

        var refused = await AddAttachmentsAsync(message, ticket, user, cancellationToken);
        _context.Tickets.Add(ticket);
        _context.TicketActivities.Add(new TicketActivity { Ticket = ticket, ActorId = user.Id, Type = TicketActivityType.Created });
        await _context.SaveChangesAsync(cancellationToken);

        Confirm(user, ticket, refused);
        return await RecordAsync(hash, user.Id, ticket.Id, InboundEmailResult.TicketCreated, null, cancellationToken);
    }

    private async Task<InboundEmailResult> AddCommentAsync(MimeMessage message, string hash, ApplicationUser user, Ticket ticket, CancellationToken cancellationToken)
    {
        var body = InboundEmailRules.WithoutQuotedReply(InboundEmailRules.Body(message));
        if (body.Length == 0)
        {
            return await RecordAsync(hash, user.Id, ticket.Id, InboundEmailResult.Skipped, "Yanıtta yeni metin yok.", cancellationToken);
        }

        _context.TicketComments.Add(new TicketComment
        {
            TicketId = ticket.Id,
            AuthorId = user.Id,
            Body = InboundEmailRules.Shorten(body, InboundEmailRules.MaxCommentLength)
        });
        ticket.UpdatedAt = DateTime.UtcNow;
        await AddAttachmentsAsync(message, ticket, user, cancellationToken);

        // The other side of the conversation hears about it
        if (user.Id == ticket.RequesterId)
        {
            Notify(ticket.AssignedToId, ticket, "talep eden yeni bir yorum yazdı.");
        }
        else
        {
            Notify(ticket.RequesterId, ticket, "BT ekibi yeni bir yorum yazdı.");
        }
        await _context.SaveChangesAsync(cancellationToken);

        return await RecordAsync(hash, user.Id, ticket.Id, InboundEmailResult.CommentAdded, null, cancellationToken);
    }

    // Same limits as the site. A file that is refused does not stop the ticket; its name goes back to the sender.
    private async Task<List<string>> AddAttachmentsAsync(MimeMessage message, Ticket ticket, ApplicationUser user, CancellationToken cancellationToken)
    {
        var refused = new List<string>();
        var saved = 0;
        foreach (var part in message.BodyParts.OfType<MimePart>().Where(p => !string.IsNullOrEmpty(p.FileName)))
        {
            using var content = new MemoryStream();
            await part.Content!.DecodeToAsync(content, cancellationToken);
            content.Position = 0;

            if (IsDecoration(part, content.Length))
            {
                continue;
            }

            var fileName = part.FileName!;
            var file = new FormFile(content, 0, content.Length, "files", fileName);
            if (saved >= AttachmentStorage.MaxFilesPerUpload || AttachmentStorage.Validate(file) != null)
            {
                refused.Add(fileName);
                continue;
            }

            ticket.Attachments.Add(await _storage.SaveAsync(file, user.Id));
            saved++;
        }
        return refused;
    }

    private static bool IsDecoration(MimePart part, long size) =>
        part.ContentType.IsMimeType("image", "*") && !part.IsAttachment && size < MinInlineImageSize;

    private void Notify(string? userId, Ticket ticket, string message)
    {
        if (userId == null)
        {
            return;
        }

        var title = ticket.Title.Length > 80 ? ticket.Title[..80] + "…" : ticket.Title;
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = $"#{ticket.Id} \"{title}\": {message}",
            Url = $"/Tickets/Details/{ticket.Id}"
        });
    }

    // The ticket number in the subject is what lets a reply to this mail find its way back to the ticket
    private void Confirm(ApplicationUser user, Ticket ticket, List<string> refused)
    {
        if (!_options.IsEnabled || user.Email == null)
        {
            return;
        }

        var text = $"\"{ticket.Title}\" talebiniz #{ticket.Id} numarasıyla açıldı. Bu e-postayı yanıtlayarak talebe yorum ekleyebilirsiniz.";
        if (refused.Count > 0)
        {
            text += $" Eklenemeyen dosyalar: {string.Join(", ", refused)}.";
        }

        var body = _options.PublicUrl is string url && url.Length > 0
            ? EmailTemplate.MessageWithButton(text, "Talebi Görüntüle", $"{url.TrimEnd('/')}/Tickets/Details/{ticket.Id}")
            : $"<p>{WebUtility.HtmlEncode(text)}</p>";
        _queue.Enqueue(new EmailMessage(user.Email, $"[#{ticket.Id}] Talebiniz alındı: {InboundEmailRules.Shorten(ticket.Title, 70)}",
            EmailTemplate.Wrap("Talebiniz alındı", body)));
    }

    private async Task<InboundEmailResult> RecordAsync(string hash, string? userId, int? ticketId, InboundEmailResult result, string? skipReason,
        CancellationToken cancellationToken)
    {
        _context.InboundEmails.Add(new InboundEmail { MessageHash = hash, UserId = userId, TicketId = ticketId, Result = result, SkipReason = skipReason });
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    // A mail without a Message-ID still gets a stable key from what it contains
    private static string Hash(MimeMessage message)
    {
        var key = message.MessageId ?? $"{message.From}|{message.Date:O}|{message.Subject}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
