namespace HelpdeskOps.Services.CompanyDirectory;

// One person as the company directory describes them. Id never changes, even when the person's name or e-mail does.
public record DirectoryUser(string Id, string Email, string FirstName, string LastName, string? Department, bool IsEnabled);

// The part of an account the sync compares
public record LocalAccount(string Id, string? DirectoryId, string? Email, string FirstName, string LastName, string? Department, bool IsClosed);

public record DirectoryUpdate(LocalAccount Account, DirectoryUser Person);

public class DirectorySyncPlan
{
    public List<DirectoryUser> ToCreate { get; } = new();

    public List<DirectoryUpdate> ToUpdate { get; } = new();

    public List<LocalAccount> ToClose { get; } = new();

    // Set when the directory's answer looks wrong; nothing may be applied then
    public string? AbortReason { get; set; }
}

// Decides what a sync should change, without touching anything, so every rule can be tested on its own.
// Accounts that were never linked to the directory, such as a local IT account, are left alone. An account that is
// closed stays closed: reopening someone is a decision for IT, not a side effect of a sync.
public static class DirectorySyncPlanner
{
    public static DirectorySyncPlan Plan(IReadOnlyCollection<LocalAccount> accounts, IReadOnlyCollection<DirectoryUser> people)
    {
        var plan = new DirectorySyncPlan();

        // An empty answer means a wrong filter or an outage, not that everybody left (data integrity)
        if (people.Count == 0)
        {
            plan.AbortReason = "Dizin hiç kullanıcı döndürmedi.";
            return plan;
        }

        var directoryIds = people.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byDirectoryId = accounts.Where(a => a.DirectoryId != null)
            .ToDictionary(a => a.DirectoryId!, StringComparer.OrdinalIgnoreCase);
        var byEmail = accounts.Where(a => a.Email != null)
            .GroupBy(a => a.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matched = new HashSet<string>();

        foreach (var person in people)
        {
            // Two directory entries cannot share one account, so the first one wins
            if (!seenIds.Add(person.Id) || !seenEmails.Add(person.Email))
            {
                continue;
            }

            var account = byDirectoryId.GetValueOrDefault(person.Id) ?? MatchByEmail(byEmail, person, directoryIds);
            if (account == null)
            {
                // A disabled person with no account has nothing to sign in to
                if (person.IsEnabled)
                {
                    plan.ToCreate.Add(person);
                }
                continue;
            }

            matched.Add(account.Id);
            if (NeedsUpdate(account, person))
            {
                plan.ToUpdate.Add(new DirectoryUpdate(account, person));
            }
            if (!person.IsEnabled && !account.IsClosed)
            {
                plan.ToClose.Add(account);
            }
        }

        // Linked accounts the directory no longer lists belong to people who were deleted there
        plan.ToClose.AddRange(accounts.Where(a => a.DirectoryId != null && !a.IsClosed && !matched.Contains(a.Id)));

        // A filter pointing at the wrong OU would otherwise lock most of the company out at once (data integrity)
        var openLinked = accounts.Count(a => a.DirectoryId != null && !a.IsClosed);
        if (plan.ToClose.Count > 2 && plan.ToClose.Count * 2 > openLinked)
        {
            plan.AbortReason = $"Eşitleme {openLinked} açık hesabın {plan.ToClose.Count} tanesini kapatacaktı; dizin ayarlarını kontrol edin.";
        }

        return plan;
    }

    // An unlinked account with the same address is the same person. A linked one is taken over only when its old
    // directory entry is gone, as after moving from Active Directory to Entra.
    private static LocalAccount? MatchByEmail(Dictionary<string, LocalAccount> byEmail, DirectoryUser person, HashSet<string> directoryIds)
    {
        var account = byEmail.GetValueOrDefault(person.Email);
        return account?.DirectoryId == null || !directoryIds.Contains(account.DirectoryId) ? account : null;
    }

    private static bool NeedsUpdate(LocalAccount account, DirectoryUser person) =>
        !string.Equals(account.DirectoryId, person.Id, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(account.Email, person.Email, StringComparison.OrdinalIgnoreCase)
        || account.FirstName != person.FirstName
        || account.LastName != person.LastName
        || account.Department != person.Department;
}
