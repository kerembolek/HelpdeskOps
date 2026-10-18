using HelpdeskOps.Models;
using Microsoft.AspNetCore.Identity;

namespace HelpdeskOps.Data;

public static class DemoDataSeeder
{
    // Local development only. These accounts are never created outside Development.
    private const string DemoPassword = "Helpdesk2026!";

    private static readonly (string Email, string FirstName, string LastName, string Department, string Role)[] DemoUsers =
    {
        ("test@helpdeskops.com", "Onur", "Güçlü", "Muhasebe", "Employee"),
        ("it@helpdeskops.com", "Bilgehan", "Sungur", "Bilgi Teknolojileri", "ITAdmin"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var demo in DemoUsers)
        {
            var user = await userManager.FindByEmailAsync(demo.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = demo.Email,
                    Email = demo.Email,
                    EmailConfirmed = true,
                    FirstName = demo.FirstName,
                    LastName = demo.LastName,
                    Department = demo.Department
                };

                var result = await userManager.CreateAsync(user, DemoPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Could not seed {demo.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            if (!await userManager.IsInRoleAsync(user, demo.Role))
                await userManager.AddToRoleAsync(user, demo.Role);
        }
    }
}