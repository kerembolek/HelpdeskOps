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
}
