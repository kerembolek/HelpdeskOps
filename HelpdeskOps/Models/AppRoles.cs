namespace HelpdeskOps.Models;

// The application's roles; every user has exactly one of them
public static class AppRoles
{
    public const string Employee = "Employee";
    public const string Manager = "Manager";
    public const string ITAdmin = "ITAdmin";

    // Ordered from least to most access; a user holding several (older data) is shown with the strongest one
    public static readonly string[] All = { Employee, Manager, ITAdmin };

    public static string DisplayName(string? role) => role switch
    {
        Employee => "Çalışan",
        Manager => "Yönetici",
        ITAdmin => "BT Personeli",
        _ => "Rol yok"
    };
}

public record UserListRow(string Id, string Name, string? Email, string? Department, string? Role, int DeviceCount, int ActiveTicketCount,
    bool TwoFactorEnabled = false, bool IsLockedOut = false, bool IsAnonymized = false, bool IsClosed = false, bool IsDirectoryLinked = false);
