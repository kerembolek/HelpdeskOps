using System.Security.Claims;

namespace HelpdeskOps.Services.CompanyDirectory;

public static class EntraSignIn
{
    public const string Scheme = "Entra";

    // The object id is Microsoft's permanent key for a person. The e-mail and username claims can change and must not
    // decide who someone is, so an account is found only through the link the directory sync stored (security).
    public static string? DirectoryId(ClaimsPrincipal principal) => principal.FindFirstValue("oid");
}
