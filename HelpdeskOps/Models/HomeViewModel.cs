namespace HelpdeskOps.Models;

// The home page: a greeting and shortcut tiles chosen for the signed-in user's roles
public class HomeViewModel
{
    public string? FirstName { get; set; }

    // Everyday tiles every signed-in user gets
    public List<HomeTile> PersonalTiles { get; set; } = new();

    // Work queues for IT staff and managers
    public List<HomeTile> TeamTiles { get; set; } = new();

    // Shown to visitors of the demo site so they can sign in and try each role
    public bool ShowDemoAccounts { get; set; }
}

// Color is a Bootstrap theme color name such as "primary" or "danger"
public record HomeTile(string Title, string Description, string Icon, string Color, string Url, int? Count = null);
