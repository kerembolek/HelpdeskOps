using System.Text;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HelpdeskOps.Tests;

public class InboundEmailRulesTests
{
    private static MimeMessage Mail(string from = "onur@x.com", string subject = "Yazıcı bozuk", string? body = "Çalışmıyor")
    {
        var message = new MimeMessage { Subject = subject };
        message.From.Add(MailboxAddress.Parse(from));
        message.Body = new BodyBuilder { TextBody = body }.ToMessageBody();
        return message;
    }

    [Fact]
    public void PersonsMailIsAccepted() => Assert.Null(InboundEmailRules.SkipReason(Mail(), "destek@x.com"));

    [Theory]
    [InlineData("Auto-Submitted", "auto-replied")]
    [InlineData("Precedence", "bulk")]
    [InlineData("List-Id", "<liste.x.com>")]
    public void MachineMailIsSkipped(string header, string value)
    {
        var mail = Mail();
        mail.Headers.Add(header, value);

        Assert.NotNull(InboundEmailRules.SkipReason(mail, "destek@x.com"));
    }

    [Fact]
    public void AutoSubmittedNoIsAPerson()
    {
        var mail = Mail();
        mail.Headers.Add("Auto-Submitted", "no");

        Assert.Null(InboundEmailRules.SkipReason(mail, "destek@x.com"));
    }

    [Theory]
    [InlineData("destek@x.com")]
    [InlineData("MAILER-DAEMON@x.com")]
    [InlineData("noreply@x.com")]
    public void OwnAndMachineAddressesAreSkipped(string from)
    {
        Assert.NotNull(InboundEmailRules.SkipReason(Mail(from), "destek@x.com"));
    }

    [Theory]
    [InlineData("mx.x.com; dmarc=fail header.from=x.com", true)]
    [InlineData("mx.x.com; spf=fail smtp.mailfrom=x.com", true)]
    [InlineData("mx.x.com; spf=fail smtp.mailfrom=x.com; dmarc=pass", false)]
    [InlineData("mx.x.com; dmarc=pass", false)]
    public void FailedSenderAuthenticationIsForged(string results, bool forged)
    {
        var mail = Mail();
        mail.Headers.Add("Authentication-Results", results);

        Assert.Equal(forged, InboundEmailRules.LooksForged(mail));
    }

    [Theory]
    [InlineData("Ynt: [#42] Talebiniz alındı: Yazıcı", 42)]
    [InlineData("Re: Yeni bildirim: #7 \"Yazıcı\": BT ekibi yorum yazdı", 7)]
    [InlineData("Yazıcı #3 bozuk", null)]
    [InlineData(null, null)]
    public void TicketNumberComesOnlyFromOurOwnSubjects(string? subject, int? id)
    {
        Assert.Equal(id, InboundEmailRules.TicketIdFromSubject(subject));
    }

    [Theory]
    [InlineData("Ynt: FW: Yazıcı bozuk", "Yazıcı bozuk")]
    [InlineData("  ", InboundEmailRules.DefaultTitle)]
    public void TitleIsCleaned(string subject, string title)
    {
        Assert.Equal(title, InboundEmailRules.Title(subject));
    }

    [Fact]
    public void QuotedReplyIsRemoved()
    {
        Assert.Equal("Oldu, teşekkürler", InboundEmailRules.WithoutQuotedReply("Oldu, teşekkürler\n\n30 Eki 2026 tarihinde HelpdeskOps şunu yazdı:\n> eski"));
        Assert.Equal("Tamam", InboundEmailRules.WithoutQuotedReply("Tamam\nFrom: destek@x.com\nSent: bugün"));
    }

    [Fact]
    public void HtmlBecomesPlainText()
    {
        var text = InboundEmailRules.PlainText("<html><head><style>p{}</style></head><body><p>Ekran &amp; yazıcı</p><script>x()</script><div>bozuk<br>yardım</div></body></html>");

        Assert.Equal("Ekran & yazıcı\nbozuk\nyardım", text);
    }
}

public class InboundEmailProcessorTests : KvkkTestDatabase
{
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "HelpdeskOps";
        public string EnvironmentName { get; set; } = "Development";
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "helpdeskops-tests-" + Guid.NewGuid().ToString("N"));
    private readonly EmailQueue _queue = new();

    private InboundEmailProcessor Processor() => new(Context, new AttachmentStorage(new TestEnvironment { ContentRootPath = _root }), _queue,
        Options.Create(new EmailOptions { Mode = EmailMode.Pickup, PublicUrl = "https://destek.test/", Inbound = new InboundEmailOptions { UserName = "destek@x.com" } }));

    private static MimeMessage Mail(string from, string subject, string body, Action<BodyBuilder>? build = null)
    {
        var message = new MimeMessage { Subject = subject, MessageId = Guid.NewGuid().ToString("N") + "@x.com" };
        message.From.Add(MailboxAddress.Parse(from));
        var builder = new BodyBuilder { TextBody = body };
        build?.Invoke(builder);
        message.Body = builder.ToMessageBody();
        return message;
    }

    private async Task<List<EmailMessage>> SentMailAsync()
    {
        var sent = new List<EmailMessage>();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        try
        {
            await foreach (var message in _queue.ReadAllAsync(cancel.Token))
            {
                sent.Add(message);
            }
        }
        catch (OperationCanceledException)
        {
        }
        return sent;
    }

    [Fact]
    public async Task KnownSenderOpensATicketAndGetsItsNumber()
    {
        var result = await Processor().ProcessAsync(Mail("ONUR@x.com", "Yazıcı bozuk", "Kağıt sıkıştı"));

        Assert.Equal(InboundEmailResult.TicketCreated, result);
        var ticket = await Context.Tickets.OrderBy(t => t.Id).LastAsync();
        Assert.Equal(("Yazıcı bozuk", "Kağıt sıkıştı", "emp-1"), (ticket.Title, ticket.Description, ticket.RequesterId));
        Assert.Equal((TicketCategory.Other, TicketPriority.Medium, TicketStatus.Open), (ticket.Category, ticket.Priority, ticket.Status));
        Assert.True(await Context.TicketActivities.AnyAsync(a => a.TicketId == ticket.Id && a.Type == TicketActivityType.Created));

        var mail = Assert.Single(await SentMailAsync());
        Assert.Equal("onur@x.com", mail.To);
        Assert.StartsWith($"[#{ticket.Id}]", mail.Subject);
        Assert.Contains($"https://destek.test/Tickets/Details/{ticket.Id}", mail.HtmlBody);
    }

    [Fact]
    public async Task UnknownSenderGetsNothing()
    {
        var count = await Context.Tickets.CountAsync();

        var result = await Processor().ProcessAsync(Mail("yabanci@y.com", "Merhaba", "Reklam"));

        Assert.Equal(InboundEmailResult.Skipped, result);
        Assert.Equal(count, await Context.Tickets.CountAsync());
        Assert.Empty(await SentMailAsync());
    }

    [Fact]
    public async Task ClosedAccountCannotOpenTickets()
    {
        var user = await Context.Users.FindAsync("emp-1");
        user!.LockoutEnd = DateTimeOffset.MaxValue;
        await Context.SaveChangesAsync();

        Assert.Equal(InboundEmailResult.Skipped, await Processor().ProcessAsync(Mail("onur@x.com", "Yeni", "Metin")));
    }

    [Fact]
    public async Task TheSameMailIsHandledOnce()
    {
        var mail = Mail("onur@x.com", "Yazıcı", "Metin");
        await Processor().ProcessAsync(mail);
        var count = await Context.Tickets.CountAsync();

        Assert.Equal(InboundEmailResult.Skipped, await Processor().ProcessAsync(mail));
        Assert.Equal(count, await Context.Tickets.CountAsync());
    }

    [Fact]
    public async Task ReplyBecomesACommentWithoutTheQuotedMail()
    {
        var ticket = await Context.Tickets.FirstAsync(t => t.RequesterId == "emp-1");

        var result = await Processor().ProcessAsync(Mail("onur@x.com", $"Ynt: [#{ticket.Id}] Talebiniz alındı", "Hâlâ bozuk\n> eski metin"));

        Assert.Equal(InboundEmailResult.CommentAdded, result);
        var comment = await Context.TicketComments.OrderBy(c => c.Id).LastAsync();
        Assert.Equal(("Hâlâ bozuk", "emp-1"), (comment.Body, comment.AuthorId));
        Assert.True(await Context.Notifications.AnyAsync(n => n.UserId == "it-1" && n.Message.Contains("yorum")));
    }

    [Fact]
    public async Task SomeoneElsesTicketNumberOpensANewTicketInstead()
    {
        var other = await Context.Tickets.FirstAsync(t => t.RequesterId == "it-1");
        var comments = await Context.TicketComments.CountAsync();

        var result = await Processor().ProcessAsync(Mail("onur@x.com", $"[#{other.Id}] Başkasının talebi", "Araya giriyorum"));

        Assert.Equal(InboundEmailResult.TicketCreated, result);
        Assert.Equal(comments, await Context.TicketComments.CountAsync());
    }

    [Fact]
    public async Task AllowedFilesAreSavedAndOthersAreReported()
    {
        var mail = Mail("onur@x.com", "Ekran görüntüsü", "Ektedir", b =>
        {
            b.Attachments.Add("hata.png", Encoding.UTF8.GetBytes("png"));
            b.Attachments.Add("virus.exe", Encoding.UTF8.GetBytes("exe"));
        });

        await Processor().ProcessAsync(mail);

        var ticket = await Context.Tickets.Include(t => t.Attachments).OrderBy(t => t.Id).LastAsync();
        Assert.Equal("hata.png", Assert.Single(ticket.Attachments).FileName);
        Assert.Contains("virus.exe", Assert.Single(await SentMailAsync()).HtmlBody);
    }

    [Fact]
    public async Task SenderHourlyLimitStopsAMailLoop()
    {
        for (var i = 0; i < InboundEmailProcessor.MaxMailsPerHour; i++)
        {
            Context.InboundEmails.Add(new InboundEmail { MessageHash = $"h{i}", UserId = "emp-1", Result = InboundEmailResult.TicketCreated });
        }
        await Context.SaveChangesAsync();

        Assert.Equal(InboundEmailResult.Skipped, await Processor().ProcessAsync(Mail("onur@x.com", "Bir tane daha", "Metin")));
    }

    public override void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
