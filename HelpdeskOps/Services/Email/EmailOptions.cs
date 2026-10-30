namespace HelpdeskOps.Services.Email;

public enum EmailMode
{
    // Nothing is sent; the live demo runs like this
    None,
    // Messages are written as .eml files, so they can be read locally without a mail server
    Pickup,
    // Messages go out through an SMTP server
    Smtp
}

// The "Email" section of appsettings; the SMTP password belongs in a git-ignored settings file or user secrets
public class EmailOptions
{
    public const string SectionName = "Email";

    public EmailMode Mode { get; set; } = EmailMode.None;

    public string FromAddress { get; set; } = "helpdesk@helpdeskops.local";

    public string FromName { get; set; } = "HelpdeskOps";

    // Relative paths are resolved against the app's content root
    public string PickupDirectory { get; set; } = Path.Combine("App_Data", "mail-outbox");

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    // The site's public address, for links in mails sent outside a web request, such as "https://helpdesk.sirket.com"
    public string? PublicUrl { get; set; }

    public InboundEmailOptions Inbound { get; set; } = new();

    public bool IsEnabled => Mode != EmailMode.None;

    // Real delivery is what makes e-mail confirmation and password reset trustworthy
    public bool DeliversRealMail => Mode == EmailMode.Smtp;
}

// The mailbox staff write to in order to open a ticket; off until Host is set
public class InboundEmailOptions
{
    public string Host { get; set; } = "";

    public int Port { get; set; } = 993;

    public bool UseSsl { get; set; } = true;

    public string UserName { get; set; } = "";

    public string Password { get; set; } = "";

    public string Folder { get; set; } = "INBOX";

    public int PollSeconds { get; set; } = 60;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Host);
}
