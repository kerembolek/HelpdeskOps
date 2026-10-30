using System.Net;
using System.Text.RegularExpressions;
using MimeKit;

namespace HelpdeskOps.Services.Email;

// What the mailbox reader decides from a message alone, so every rule can be tested without a mail server
public static partial class InboundEmailRules
{
    public const int MaxDescriptionLength = 10000;
    public const int MaxCommentLength = 2000;
    public const int MaxTitleLength = 200;
    public const string DefaultTitle = "E-postayla açılan talep";

    private static readonly string[] MachineSenders = { "mailer-daemon", "postmaster", "noreply", "no-reply", "donotreply" };
    private static readonly string[] BulkPrecedence = { "bulk", "junk", "list", "auto_reply" };

    // [#123] in the subject of our own mails, or the "#123" that notification subjects start with
    [GeneratedRegex(@"\[#(\d{1,9})\]|^(?:\S+:\s*)*Yeni bildirim: #(\d{1,9})\b", RegexOptions.IgnoreCase)]
    private static partial Regex TicketNumber();

    [GeneratedRegex(@"^\s*((re|fw|fwd|ynt|ilt|yanıt|ilet)\s*:\s*)+", RegexOptions.IgnoreCase)]
    private static partial Regex ReplyPrefix();

    [GeneratedRegex(@"\[#\d{1,9}\]\s*")]
    private static partial Regex TicketTag();

    // Where a mail program starts quoting the message being answered
    [GeneratedRegex(@"^(>|-{2,}\s*(original message|özgün ileti|orijinal ileti)|(from|kimden):\s|on .+ wrote:\s*$|.+ tarihinde .+ yazdı:\s*$)", RegexOptions.IgnoreCase)]
    private static partial Regex QuoteStart();

    [GeneratedRegex(@"<(style|script|head)\b.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HiddenHtml();

    [GeneratedRegex(@"<(br|/p|/div|/li|/tr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakHtml();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    // Returns why a message must not become a ticket, or null when it may. Answering a machine is how mail loops start.
    public static string? SkipReason(MimeMessage message, params string[] ownAddresses)
    {
        var mailboxes = message.From.Mailboxes.ToList();
        if (mailboxes.Count != 1)
            return "Gönderen tek bir adres değil.";

        var address = mailboxes[0].Address;
        if (ownAddresses.Contains(address, StringComparer.OrdinalIgnoreCase))
            return "Kendi adresimizden gelmiş.";
        if (MachineSenders.Contains(address.Split('@')[0], StringComparer.OrdinalIgnoreCase))
            return "Otomatik gönderen.";

        var autoSubmitted = message.Headers[HeaderId.AutoSubmitted];
        if (!string.IsNullOrEmpty(autoSubmitted) && !autoSubmitted.Trim().Equals("no", StringComparison.OrdinalIgnoreCase))
            return "Otomatik yanıt.";
        if (BulkPrecedence.Contains(message.Headers[HeaderId.Precedence]?.Trim(), StringComparer.OrdinalIgnoreCase))
            return "Toplu e-posta.";
        if (message.Headers.Contains(HeaderId.ListId))
            return "E-posta listesi.";

        return LooksForged(message) ? "Gönderen doğrulaması başarısız." : null;
    }

    // Our mail server adds the newest Authentication-Results at the top. A failure there means the From address is not
    // who it claims to be; a mail with no such header says nothing, because internal mail often has none (security).
    public static bool LooksForged(MimeMessage message)
    {
        var results = message.Headers[HeaderId.AuthenticationResults]?.ToLowerInvariant();
        if (string.IsNullOrEmpty(results))
            return false;

        return results.Contains("dmarc=fail") || (!results.Contains("dmarc=") && results.Contains("spf=fail"));
    }

    public static int? TicketIdFromSubject(string? subject)
    {
        var match = TicketNumber().Match(subject ?? "");
        if (!match.Success)
            return null;

        return int.TryParse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value, out var id) ? id : null;
    }

    public static string Title(string? subject)
    {
        var title = TicketTag().Replace(ReplyPrefix().Replace(subject ?? "", ""), "").Trim();
        if (title.Length == 0)
            return DefaultTitle;
        return title.Length > MaxTitleLength ? title[..(MaxTitleLength - 1)] + "…" : title;
    }

    public static string Body(MimeMessage message)
    {
        var text = message.TextBody ?? PlainText(message.HtmlBody ?? "");
        return text.Replace("\r\n", "\n").Trim();
    }

    public static string PlainText(string html)
    {
        var text = LineBreakHtml().Replace(HiddenHtml().Replace(html, ""), "\n");
        return BlankLines().Replace(WebUtility.HtmlDecode(AnyTag().Replace(text, "")), "\n\n").Trim();
    }

    // Keeps only what the person wrote above the quoted message
    public static string WithoutQuotedReply(string body)
    {
        var lines = body.Split('\n');
        var end = Array.FindIndex(lines, line => QuoteStart().IsMatch(line.Trim()));
        return string.Join('\n', end < 0 ? lines : lines[..end]).Trim();
    }

    public static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";
}
