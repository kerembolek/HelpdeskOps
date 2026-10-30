namespace HelpdeskOps.Services.CompanyDirectory;

// Reads everybody from the company directory
public interface IDirectoryClient
{
    Task<IReadOnlyList<DirectoryUser>> GetUsersAsync(CancellationToken cancellationToken);
}

// Asks the company directory whether a password is right
public interface IDirectoryPasswordChecker
{
    Task<bool> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);
}

// The directory could not be reached, which is different from "wrong password": it must not count toward a lockout
public class DirectoryUnavailableException : Exception
{
    public DirectoryUnavailableException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}

public static class DirectoryNames
{
    // Directories may leave the first or last name empty, but accounts need both
    public static (string First, string Last) Split(string? firstName, string? lastName, string? displayName, string email)
    {
        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
        {
            return (firstName.Trim(), lastName.Trim());
        }

        var parts = (displayName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            ? (string.Join(' ', parts[..^1]), parts[^1])
            : (parts.Length == 1 ? parts[0] : email.Split('@')[0], "-");
    }
}
