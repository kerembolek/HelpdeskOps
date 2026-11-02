using HelpdeskOps.Services.CompanyDirectory;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services;

public class RegistrationOptions
{
    public const string SectionName = "Registration";

    // The company's e-mail domains, such as "sirket.com.tr"; "*" allows any address, for local development
    public List<string> AllowedDomains { get; set; } = new();
}

// Decides who can sign themselves up. Registration is closed unless the settings open it, so a site that reaches the
// internet does not hand an account to anyone who finds it. The "InitialAdminEmail" address can always register,
// because a fresh install needs that account for its first IT staff member.
public class RegistrationPolicy
{
    private const string AnyDomain = "*";

    private readonly List<string> _domains;
    private readonly string? _initialAdminEmail;
    private readonly bool _blocked;

    public RegistrationPolicy(IOptions<RegistrationOptions> options, IOptions<DirectoryOptions> directory, IConfiguration configuration)
    {
        // Docker passes an empty value when the setting is left out
        _domains = options.Value.AllowedDomains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(domain => domain.Trim().TrimStart('@'))
            .ToList();
        var initialAdminEmail = configuration[InitialAdmin.SettingName];
        _initialAdminEmail = string.IsNullOrWhiteSpace(initialAdminEmail) ? null : initialAdminEmail.Trim();
        // With a company directory, accounts come from the directory; the read-only demo refuses every write
        _blocked = directory.Value.IsEnabled || DemoReadOnlyMiddleware.IsEnabled(configuration);
    }

    public bool IsOpen => !_blocked && (_domains.Count > 0 || _initialAdminEmail != null);

    public bool Allows(string email)
    {
        if (!IsOpen)
        {
            return false;
        }

        var address = email.Trim();
        if (string.Equals(address, _initialAdminEmail, StringComparison.OrdinalIgnoreCase) || _domains.Contains(AnyDomain))
        {
            return true;
        }

        var domain = address[(address.LastIndexOf('@') + 1)..];
        return _domains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }
}
