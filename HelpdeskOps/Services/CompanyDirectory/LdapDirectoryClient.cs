using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.CompanyDirectory;

// Talks to on-premises Active Directory over LDAP. A service account reads the users; a password is checked by finding
// the person with the service account and then binding as them, so the directory itself judges the password.
public class LdapDirectoryClient : IDirectoryClient, IDirectoryPasswordChecker
{
    private const string PeopleFilter = "(&(objectCategory=person)(objectClass=user))";
    private const int PageSize = 500;
    // The LDAP result code for a wrong password
    private const int InvalidCredentials = 49;
    // The ACCOUNTDISABLE bit of userAccountControl
    private const int AccountDisabled = 2;

    private static readonly string[] Attributes =
    {
        "objectGUID", "givenName", "sn", "displayName", "mail", "userPrincipalName", "department", "userAccountControl"
    };

    private readonly LdapOptions _options;

    public LdapDirectoryClient(IOptions<DirectoryOptions> options)
    {
        _options = options.Value.Ldap;
    }

    public Task<IReadOnlyList<DirectoryUser>> GetUsersAsync(CancellationToken cancellationToken) =>
        // LdapConnection has no real async API, so the blocking calls stay off the request thread
        Task.Run(() => Guard(() =>
        {
            using var connection = Connect();
            connection.Bind(new NetworkCredential(_options.BindDn, _options.BindPassword));

            var people = new List<DirectoryUser>();
            var paging = new PageResultRequestControl(PageSize) { IsCritical = false };
            var request = new SearchRequest(_options.BaseDn, PeopleFilter, SearchScope.Subtree, Attributes);
            request.Controls.Add(paging);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = (SearchResponse)connection.SendRequest(request);
                people.AddRange(response.Entries.Cast<SearchResultEntry>().Select(ToPerson).OfType<DirectoryUser>());

                var page = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault();
                if (page == null || page.Cookie.Length == 0)
                {
                    return (IReadOnlyList<DirectoryUser>)people;
                }
                paging.Cookie = page.Cookie;
            }
        }), cancellationToken);

    public Task<bool> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            // LDAP treats a bind with an empty password as an anonymous sign-in and reports success (security)
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            return Guard(() =>
            {
                using var connection = Connect();
                connection.Bind(new NetworkCredential(_options.BindDn, _options.BindPassword));

                var filter = $"(&{PeopleFilter}(|(mail={EscapeFilter(email)})(userPrincipalName={EscapeFilter(email)})))";
                var search = new SearchRequest(_options.BaseDn, filter, SearchScope.Subtree, "distinguishedName");
                var entries = ((SearchResponse)connection.SendRequest(search)).Entries;
                if (entries.Count != 1)
                {
                    return false;
                }

                try
                {
                    connection.Bind(new NetworkCredential(entries[0].DistinguishedName, password));
                    return true;
                }
                catch (LdapException ex) when (ex.ErrorCode == InvalidCredentials)
                {
                    return false;
                }
            });
        }, cancellationToken);

    // Keeps a typed address from changing the search, for example "*" matching everybody (security)
    public static string EscapeFilter(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            escaped.Append(character switch
            {
                '\\' => @"\5c",
                '*' => @"\2a",
                '(' => @"\28",
                ')' => @"\29",
                '\0' => @"\00",
                _ => character.ToString()
            });
        }
        return escaped.ToString();
    }

    public static bool IsEnabled(string? userAccountControl) =>
        !int.TryParse(userAccountControl, out var flags) || (flags & AccountDisabled) == 0;

    private LdapConnection Connect()
    {
        var connection = new LdapConnection(new LdapDirectoryIdentifier(_options.Host, _options.Port)) { AuthType = AuthType.Basic };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
        // Active Directory answers with referrals to other servers, which only slow the search down here
        connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        connection.Timeout = TimeSpan.FromSeconds(15);
        return connection;
    }

    private static T Guard<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException)
        {
            throw new DirectoryUnavailableException("Active Directory sunucusuna ulaşılamadı.", ex);
        }
    }

    // Entries with no address cannot sign in, which also skips built-in accounts
    private static DirectoryUser? ToPerson(SearchResultEntry entry)
    {
        var email = Text(entry, "mail") ?? Text(entry, "userPrincipalName");
        if (email == null || !entry.Attributes.Contains("objectGUID") || entry.Attributes["objectGUID"].GetValues(typeof(byte[])) is not [byte[] { Length: 16 } guid])
        {
            return null;
        }

        var (first, last) = DirectoryNames.Split(Text(entry, "givenName"), Text(entry, "sn"), Text(entry, "displayName"), email);
        return new DirectoryUser(new Guid(guid).ToString(), email, first, last, Text(entry, "department"), IsEnabled(Text(entry, "userAccountControl")));
    }

    private static string? Text(SearchResultEntry entry, string name)
    {
        if (!entry.Attributes.Contains(name) || entry.Attributes[name].GetValues(typeof(string)) is not [string value, ..] || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Trim();
    }
}
