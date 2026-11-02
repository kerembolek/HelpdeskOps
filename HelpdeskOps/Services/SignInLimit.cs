using System.Threading.RateLimiting;

namespace HelpdeskOps.Services;

// Account lockout stops guessing against one account; this stops one address from trying many accounts. Only posts
// count, and the limit stays well above a full office signing in together from one address at nine o'clock.
public static class SignInLimit
{
    public const string PolicyName = "SignIn";
    public const int PermitLimit = 30;

    public static RateLimitPartition<string> Partition(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("page views");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitLimit, Window = TimeSpan.FromMinutes(1) });
    }
}
