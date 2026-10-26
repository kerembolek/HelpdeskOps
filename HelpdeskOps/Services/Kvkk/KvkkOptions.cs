namespace HelpdeskOps.Services.Kvkk;

// The "Kvkk" settings section: who the data controller is, for the privacy notice (aydınlatma metni) under
// KVKK (Law No. 6698). Each company that installs HelpdeskOps fills in its own details.
public class KvkkOptions
{
    public const string SectionName = "Kvkk";

    // Raise this when the notice text changes, so everyone is asked to read it again
    public string NoticeVersion { get; set; } = "2026-10";

    public string ControllerName { get; set; } = "Şirket Adı";

    public string? ControllerAddress { get; set; }

    // Where people send KVKK requests (article 11)
    public string? ContactEmail { get; set; }

    public RetentionOptions Retention { get; set; } = new();
}

// How long personal data is kept, in days; 0 turns a rule off. RetentionCleaner applies them once a day.
public class RetentionOptions
{
    // Every in-app notification, read or not
    public int NotificationDays { get; set; } = 180;

    // Files attached to tickets, counted from when the ticket was resolved or closed
    public int AttachmentDays { get; set; } = 730;

    // IP addresses in the audit log; two years, like the traffic logs Law No. 5651 asks for
    public int AuditIpAddressDays { get; set; } = 730;

    // Audit log entries themselves
    public int AuditLogDays { get; set; } = 3650;

    // Names, notes, and signatures on returned zimmet forms, counted from the return; ten years is the general
    // limitation period for claims under the Turkish Code of Obligations
    public int AssignmentFormDays { get; set; } = 3650;
}
