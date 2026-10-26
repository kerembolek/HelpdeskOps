using HelpdeskOps.Models;
using Microsoft.AspNetCore.Identity;

namespace HelpdeskOps.Services;

// A fresh install has no IT staff, and only IT staff can change roles. With "InitialAdminEmail" set, the startup
// makes that registered user the first IT staff member. Once anyone holds the IT role the setting does nothing,
// so leaving it in the settings cannot be used to take over an account later.
public static class InitialAdmin
{
    public const string SettingName = "InitialAdminEmail";

    // Returns what happened, for the startup log
    public static async Task<string?> EnsureAsync(UserManager<ApplicationUser> userManager, string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || (await userManager.GetUsersInRoleAsync(AppRoles.ITAdmin)).Count > 0)
        {
            return null;
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user == null || user.AnonymizedAt != null)
        {
            return $"{SettingName} is set to {email}, but no such user exists yet; register that account first and restart the app.";
        }

        // Every user holds exactly one role
        await userManager.RemoveFromRolesAsync(user, (await userManager.GetRolesAsync(user)).Intersect(AppRoles.All));
        await userManager.AddToRoleAsync(user, AppRoles.ITAdmin);
        return $"{email} is now the first IT staff member ({SettingName}).";
    }
}
