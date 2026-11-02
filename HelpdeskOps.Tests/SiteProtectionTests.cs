using System.Net;
using System.Text.RegularExpressions;
using HelpdeskOps.Services;

namespace HelpdeskOps.Tests;

public class SiteProtectionTests : IClassFixture<TestApp>
{
    private const string LoginPage = "/Identity/Account/Login";

    private readonly TestApp _app;

    public SiteProtectionTests(TestApp app)
    {
        _app = app;
    }

    [Fact]
    public async Task Pages_carry_the_security_headers()
    {
        var response = await _app.ClientFor().GetAsync(LoginPage);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.Contains("form-action 'self'", policy);
    }

    [Fact]
    public async Task Scripts_need_the_nonce_of_their_own_page()
    {
        var response = await _app.ClientFor().GetAsync(LoginPage);

        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        var scriptPolicy = policy.Split("; ").Single(part => part.StartsWith("script-src "));
        Assert.DoesNotContain("unsafe-inline", scriptPolicy);

        var nonce = Regex.Match(scriptPolicy, "'nonce-([^']+)'").Groups[1].Value;
        var page = await response.Content.ReadAsStringAsync();
        Assert.All(Regex.Matches(page, "<script(?![^>]*\\ssrc=)[^>]*>").Select(tag => tag.Value),
            tag => Assert.Contains($"nonce=\"{nonce}\"", tag));
    }

    [Fact]
    public async Task Pages_load_scripts_only_from_this_site()
    {
        var page = await (await _app.ClientFor().GetAsync(LoginPage)).Content.ReadAsStringAsync();

        Assert.DoesNotMatch("<script[^>]*src=\"https?:", page);
    }

    [Fact]
    public async Task Nonces_change_between_requests()
    {
        var first = (await _app.ClientFor().GetAsync(LoginPage)).Headers.GetValues("Content-Security-Policy").Single();
        var second = (await _app.ClientFor().GetAsync(LoginPage)).Headers.GetValues("Content-Security-Policy").Single();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Health_reports_only_its_status()
    {
        var response = await _app.ClientFor().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_sign_up_page_is_closed_by_default()
    {
        var response = await _app.ClientFor().GetAsync("/Identity/Account/Register");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
    }
}

// In its own class, because it uses up the limit of the app it runs against
public class SignInLimitTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;

    public SignInLimitTests(TestApp app)
    {
        _app = app;
    }

    [Fact]
    public async Task Too_many_sign_in_posts_are_refused_but_the_page_still_opens()
    {
        var client = _app.ClientFor();
        var empty = new FormUrlEncodedContent(new Dictionary<string, string>());
        for (var attempt = 0; attempt < SignInLimit.PermitLimit; attempt++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.PostAsync("/Identity/Account/Login", empty)).StatusCode);
        }

        var refused = await client.PostAsync("/Identity/Account/Login", empty);
        var page = await client.GetAsync("/Identity/Account/Login");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }
}
