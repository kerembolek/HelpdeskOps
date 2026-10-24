using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HelpdeskOps.Services;

// Safety net for the public live demo. With "DemoReadOnly": true every write request is refused, so visitors can
// open every screen with the published demo accounts but cannot change, delete, upload, sign, or register anything.
public class DemoReadOnlyMiddleware
{
    public const string SettingName = "DemoReadOnly";
    public const string MessageKey = "DemoReadOnlyMessage";

    // Posts that only sign in or out, or that skip their write while the demo is read-only
    private static readonly PathString[] AllowedPosts =
    {
        "/Identity/Account/Login", "/Identity/Account/Logout", "/Solutions/Escalate"
    };

    // The only Identity pages a visitor needs; registration, password reset, and account management stay closed
    private static readonly PathString[] AllowedIdentityPages =
    {
        "/Identity/Account/Login", "/Identity/Account/Logout", "/Identity/Account/AccessDenied", "/Identity/Account/Lockout"
    };

    private readonly RequestDelegate _next;

    public DemoReadOnlyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(SettingName);

    public async Task InvokeAsync(HttpContext context, ITempDataDictionaryFactory tempDataFactory)
    {
        var path = context.Request.Path;
        var method = context.Request.Method;
        var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        var blocked = isRead
            ? path.StartsWithSegments("/Identity", StringComparison.OrdinalIgnoreCase) && !Matches(path, AllowedIdentityPages)
            : !Matches(path, AllowedPosts);
        if (!blocked)
        {
            await _next(context);
            return;
        }

        var tempData = tempDataFactory.GetTempData(context);
        tempData[MessageKey] = "Bu bir demo sürümüdür; değişiklikler kaydedilmez. Tüm ekranları gezebilirsiniz.";
        tempData.Save();
        context.Response.Redirect(LocalReferrer(context) ?? "/");
    }

    // Whole segments only, so "/Solutions/Escalate/5" matches but "/Identity/Account/LoginWith2fa" does not
    private static bool Matches(PathString path, PathString[] allowed) =>
        allowed.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    // Back to the page the form was on, but never to another site
    private static string? LocalReferrer(HttpContext context)
    {
        var referrer = context.Request.Headers.Referer.ToString();
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // A blocked Identity page would only redirect again, so fall back to the home page
        return uri.AbsolutePath.StartsWith("/Identity", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath != "/Identity/Account/Login"
            ? null
            : uri.PathAndQuery;
    }
}
