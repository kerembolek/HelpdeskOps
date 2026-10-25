using HelpdeskOps.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HelpdeskOps.Tests;

public class DemoReadOnlyMiddlewareTests
{
    // Keeps TempData in memory so the refusal notice can be checked without cookies
    private sealed class MemoryTempDataFactory : ITempDataDictionaryFactory, ITempDataProvider
    {
        public TempDataDictionary? Last { get; private set; }

        public ITempDataDictionary GetTempData(HttpContext context) => Last = new TempDataDictionary(context, this);

        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static async Task<(bool PassedOn, HttpContext Context, MemoryTempDataFactory TempData)> RunAsync(
        string method, string path, string? referer = null)
    {
        var passedOn = false;
        var middleware = new DemoReadOnlyMiddleware(_ => { passedOn = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Host = new HostString("helpdeskops.runasp.net");
        if (referer != null)
        {
            context.Request.Headers.Referer = referer;
        }

        var tempData = new MemoryTempDataFactory();
        await middleware.InvokeAsync(context, tempData);
        return (passedOn, context, tempData);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Tickets/Details/5")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/AccessDenied")]
    public async Task Get_PagesVisitorsNeed_PassThrough(string path)
    {
        var (passedOn, _, _) = await RunAsync("GET", path);

        Assert.True(passedOn);
    }

    [Theory]
    [InlineData("/Identity/Account/Register")]
    [InlineData("/Identity/Account/Manage/ChangePassword")]
    [InlineData("/Identity/Account/ForgotPassword")]
    [InlineData("/Identity/Account/LoginWith2fa")]
    public async Task Get_ClosedIdentityPages_RedirectHome(string path)
    {
        var (passedOn, context, _) = await RunAsync("GET", path);

        Assert.False(passedOn);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/", context.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Logout")]
    [InlineData("/Solutions/Escalate/5")]
    public async Task Post_SignInSignOutAndEscalate_PassThrough(string path)
    {
        var (passedOn, _, _) = await RunAsync("POST", path);

        Assert.True(passedOn);
    }

    [Fact]
    public async Task Post_AnyWrite_IsRefusedAndSentBackWithANotice()
    {
        var (passedOn, context, tempData) = await RunAsync("POST", "/Tickets/AddComment/5",
            "https://helpdeskops.runasp.net/Tickets/Details/5?tab=comments");

        Assert.False(passedOn);
        Assert.Equal("/Tickets/Details/5?tab=comments", context.Response.Headers.Location.ToString());
        Assert.NotNull(tempData.Last?[DemoReadOnlyMiddleware.MessageKey]);
    }

    [Fact]
    public async Task Post_WithAnotherSitesReferer_GoesHome()
    {
        var (_, context, _) = await RunAsync("POST", "/Devices/Delete/1", "https://evil.example/page");

        Assert.Equal("/", context.Response.Headers.Location.ToString());
    }
}
