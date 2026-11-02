using HelpdeskOps.Services;
using HelpdeskOps.Services.CompanyDirectory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class RegistrationPolicyTests
{
    private static RegistrationPolicy Policy(string[]? domains = null, string? initialAdmin = null,
        DirectoryProvider provider = DirectoryProvider.None, bool demo = false)
    {
        var settings = new Dictionary<string, string?>
        {
            [InitialAdmin.SettingName] = initialAdmin,
            [DemoReadOnlyMiddleware.SettingName] = demo ? "true" : "false"
        };
        return new RegistrationPolicy(
            Options.Create(new RegistrationOptions { AllowedDomains = (domains ?? Array.Empty<string>()).ToList() }),
            Options.Create(new DirectoryOptions { Provider = provider }),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public void Registration_is_closed_by_default()
    {
        var policy = Policy();

        Assert.False(policy.IsOpen);
        Assert.False(policy.Allows("someone@example.com"));
    }

    [Fact]
    public void A_blank_domain_from_docker_keeps_registration_closed()
    {
        Assert.False(Policy(new[] { "", "  " }).IsOpen);
    }

    [Theory]
    [InlineData("ali@sirket.com.tr", true)]
    [InlineData("ALI@SIRKET.COM.TR", true)]
    [InlineData("ali@baska.com.tr", false)]
    [InlineData("ali@sirket.com.tr.evil.example", false)]
    [InlineData("ali@alt.sirket.com.tr", false)]
    public void Only_listed_domains_can_register(string email, bool allowed)
    {
        Assert.Equal(allowed, Policy(new[] { "sirket.com.tr" }).Allows(email));
    }

    [Fact]
    public void A_leading_at_sign_in_the_setting_is_ignored()
    {
        Assert.True(Policy(new[] { "@sirket.com.tr" }).Allows("ali@sirket.com.tr"));
    }

    [Fact]
    public void The_initial_admin_can_register_while_registration_is_otherwise_closed()
    {
        var policy = Policy(initialAdmin: "bt@sirket.com.tr");

        Assert.True(policy.IsOpen);
        Assert.True(policy.Allows("BT@sirket.com.tr"));
        Assert.False(policy.Allows("ali@sirket.com.tr"));
    }

    [Fact]
    public void The_wildcard_allows_any_address()
    {
        Assert.True(Policy(new[] { "*" }).Allows("someone@example.com"));
    }

    [Fact]
    public void A_company_directory_closes_registration()
    {
        Assert.False(Policy(new[] { "*" }, provider: DirectoryProvider.ActiveDirectory).IsOpen);
    }

    [Fact]
    public void The_read_only_demo_closes_registration()
    {
        Assert.False(Policy(new[] { "*" }, demo: true).IsOpen);
    }
}
