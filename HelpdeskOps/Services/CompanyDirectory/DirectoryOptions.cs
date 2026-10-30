namespace HelpdeskOps.Services.CompanyDirectory;

public enum DirectoryProvider
{
    // Accounts live only in this app; the live demo runs like this
    None,
    // Microsoft Entra ID: sign-in through Microsoft, users read from Microsoft Graph
    Entra,
    // On-premises Active Directory: passwords and users checked over LDAP
    ActiveDirectory
}

// The "Directory" section of appsettings; the client secret and bind password belong in a git-ignored settings file,
// environment variables, or user secrets
public class DirectoryOptions
{
    public const string SectionName = "Directory";

    public DirectoryProvider Provider { get; set; } = DirectoryProvider.None;

    public EntraOptions Entra { get; set; } = new();

    public LdapOptions Ldap { get; set; } = new();

    public bool IsEnabled => Provider != DirectoryProvider.None;

    // Returns what is wrong with the settings, or null when they are usable, so a bad install stops at startup
    public string? Validate(bool isDevelopment) => Provider switch
    {
        DirectoryProvider.Entra => Entra.Validate(),
        DirectoryProvider.ActiveDirectory => Ldap.Validate(isDevelopment),
        _ => null
    };
}

public class EntraOptions
{
    // The company's own tenant. "common" and friends would let any Microsoft account reach the sign-in (security)
    private static readonly string[] SharedTenants = { "common", "organizations", "consumers" };

    public string TenantId { get; set; } = "";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    // Differ only on national clouds
    public string Instance { get; set; } = "https://login.microsoftonline.com";

    public string GraphEndpoint { get; set; } = "https://graph.microsoft.com";

    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantId) || string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
        {
            return "Directory:Entra needs TenantId, ClientId, and ClientSecret.";
        }

        return SharedTenants.Contains(TenantId.Trim(), StringComparer.OrdinalIgnoreCase)
            ? "Directory:Entra:TenantId must be the company's own tenant, not a shared one such as \"common\"."
            : null;
    }
}

public class LdapOptions
{
    public string Host { get; set; } = "";

    public int Port { get; set; } = 636;

    public bool UseSsl { get; set; } = true;

    // A read-only service account, for example CN=helpdesk-sync,OU=Service,DC=sirket,DC=local
    public string BindDn { get; set; } = "";

    public string BindPassword { get; set; } = "";

    // Only users under this entry are read, so it can point at the OU that holds real people
    public string BaseDn { get; set; } = "";

    public string? Validate(bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(BindDn) || string.IsNullOrWhiteSpace(BindPassword)
            || string.IsNullOrWhiteSpace(BaseDn))
        {
            return "Directory:Ldap needs Host, BindDn, BindPassword, and BaseDn.";
        }

        // Without TLS every employee's password would cross the network in clear text (security)
        return UseSsl || isDevelopment ? null : "Directory:Ldap:UseSsl may only be turned off in the Development environment.";
    }
}
