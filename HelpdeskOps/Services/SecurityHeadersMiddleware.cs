using System.Security.Cryptography;
using HelpdeskOps.Services.CompanyDirectory;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services;

// Tells browsers how to protect this site. The Content-Security-Policy runs only our own scripts: an injected
// <script> tag has no valid nonce, so it stays dead even if some text ever slipped past Razor's encoding.
public class SecurityHeadersMiddleware
{
    private const string NonceKey = "CspNonce";
    private const string IconCdn = "https://cdn.jsdelivr.net";

    private readonly RequestDelegate _next;
    private readonly string _formAction;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<DirectoryOptions> directory)
    {
        _next = next;
        // Browsers apply form-action to redirects too, and the Microsoft sign-in button posts here first
        _formAction = directory.Value.Provider == DirectoryProvider.Entra
            ? "'self' " + new Uri(directory.Value.Entra.Authority).GetLeftPart(UriPartial.Authority)
            : "'self'";
    }

    // Inline scripts carry this value in their nonce attribute
    public static string Nonce(HttpContext context) => (string)context.Items[NonceKey]!;

    public Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceKey] = nonce;

        // Set late, because the error page clears the headers of the failed response
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers.ContentSecurityPolicy = string.Join("; ",
                "default-src 'self'",
                $"script-src 'self' 'nonce-{nonce}'",
                $"style-src 'self' 'unsafe-inline' {IconCdn}",
                $"font-src 'self' {IconCdn}",
                "img-src 'self' data:",
                "object-src 'none'",
                "base-uri 'self'",
                "frame-ancestors 'none'",
                $"form-action {_formAction}");
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
