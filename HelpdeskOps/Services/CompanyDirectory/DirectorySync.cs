using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Services.CompanyDirectory;

public record DirectorySyncResult(int Created, int Updated, int Closed, IReadOnlyList<string> Errors, string? AbortReason = null)
{
    public int Total => Created + Updated + Closed;
}

// Applies a DirectorySyncPlanner plan: new people get an Employee account with no password, because they sign in
// through the directory; roles stay in this app. Each run that changes anything leaves one summary in the audit log.
public class DirectorySync
{
    public const string AuditEntityType = "DirectorySync";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDirectoryClient _client;

    public DirectorySync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IDirectoryClient client)
    {
        _context = context;
        _userManager = userManager;
        _client = client;
    }

    // Throws DirectoryUnavailableException when the directory cannot be read
    public async Task<DirectorySyncResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var people = await _client.GetUsersAsync(cancellationToken);

        // Anonymized accounts are out of reach: their directory link and address are already gone
        var accounts = await _context.Users.AsNoTracking()
            .Where(u => u.AnonymizedAt == null)
            .Select(u => new { u.Id, u.DirectoryId, u.Email, u.FirstName, u.LastName, u.Department, u.LockoutEnd })
            .ToListAsync(cancellationToken);

        var plan = DirectorySyncPlanner.Plan(
            accounts.Select(a => new LocalAccount(a.Id, a.DirectoryId, a.Email, a.FirstName, a.LastName, a.Department,
                a.LockoutEnd == DateTimeOffset.MaxValue)).ToList(),
            people);
        if (plan.AbortReason != null)
        {
            return new DirectorySyncResult(0, 0, 0, Array.Empty<string>(), plan.AbortReason);
        }

        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var closed = 0;

        foreach (var person in plan.ToCreate)
        {
            created += await CreateAsync(person, errors) ? 1 : 0;
        }
        foreach (var update in plan.ToUpdate)
        {
            updated += await UpdateAsync(update, errors) ? 1 : 0;
        }
        foreach (var account in plan.ToClose)
        {
            closed += await CloseAsync(account, errors) ? 1 : 0;
        }

        var result = new DirectorySyncResult(created, updated, closed, errors);
        if (result.Total > 0)
        {
            await RecordAsync(result);
        }
        return result;
    }

    private async Task<bool> CreateAsync(DirectoryUser person, List<string> errors)
    {
        var user = new ApplicationUser
        {
            UserName = person.Email,
            Email = person.Email,
            EmailConfirmed = true,
            FirstName = person.FirstName,
            LastName = person.LastName,
            Department = person.Department,
            DirectoryId = person.Id
        };

        var result = await _userManager.CreateAsync(user);
        if (result.Succeeded)
        {
            result = await _userManager.AddToRoleAsync(user, AppRoles.Employee);
        }
        return Report(result, person.Email, errors);
    }

    private async Task<bool> UpdateAsync(DirectoryUpdate update, List<string> errors)
    {
        var user = await _userManager.FindByIdAsync(update.Account.Id);
        if (user == null)
        {
            return false;
        }

        var person = update.Person;
        user.DirectoryId = person.Id;
        user.UserName = person.Email;
        user.Email = person.Email;
        user.EmailConfirmed = true;
        user.FirstName = person.FirstName;
        user.LastName = person.LastName;
        user.Department = person.Department;
        return Report(await _userManager.UpdateAsync(user), person.Email, errors);
    }

    // The same closed state a completed offboarding leaves, and it ends every open sign-in
    private async Task<bool> CloseAsync(LocalAccount account, List<string> errors)
    {
        var user = await _userManager.FindByIdAsync(account.Id);
        if (user == null)
        {
            return false;
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            result = await _userManager.UpdateSecurityStampAsync(user);
        }
        return Report(result, account.Email ?? account.Id, errors);
    }

    private static bool Report(IdentityResult result, string who, List<string> errors)
    {
        if (!result.Succeeded)
        {
            errors.Add($"{who}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        return result.Succeeded;
    }

    private async Task RecordAsync(DirectorySyncResult result)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            UserName = "Dizin eşitleme",
            Action = AuditAction.Updated,
            EntityType = AuditEntityType,
            EntityLabel = $"{result.Created} yeni, {result.Updated} güncellenen, {result.Closed} kapatılan hesap"
        });
        await _context.SaveChangesAsync();
    }
}
