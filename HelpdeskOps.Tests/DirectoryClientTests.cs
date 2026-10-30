using System.Net;
using System.Text;
using HelpdeskOps.Services.CompanyDirectory;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class DirectoryClientTests
{
    private sealed class FakeGraph : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            var body = url.Contains("/token") ? """{"access_token":"t"}"""
                : url.Contains("page2") ? """{"value":[{"id":"2","mail":"b@x.com","displayName":"Mehmet Ali Demir","accountEnabled":false}]}"""
                : """
                  {"@odata.nextLink":"https://graph.test/v1.0/users?page2",
                   "value":[
                     {"id":"1","userPrincipalName":"a@x.com","givenName":"Ada","surname":"Kaya","department":"Satış","accountEnabled":true,"userType":"Member"},
                     {"id":"3","mail":"misafir@y.com","accountEnabled":true,"userType":"Guest"},
                     {"id":"5","userPrincipalName":"sahip_hotmail.com#EXT#@x.onmicrosoft.com","accountEnabled":true,"userType":"Member"},
                     {"id":"6","mail":"sahip@hotmail.com","userPrincipalName":"sahip_hotmail.com#EXT#@x.onmicrosoft.com","accountEnabled":true,"userType":"Member"},
                     {"id":"4","accountEnabled":true}]}
                  """;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static GraphDirectoryClient Client(FakeGraph graph) => new(new HttpClient(graph), Options.Create(new DirectoryOptions
    {
        Entra = new EntraOptions { TenantId = "t", ClientId = "c", ClientSecret = "s", Instance = "https://login.test", GraphEndpoint = "https://graph.test" }
    }));

    [Fact]
    public async Task GraphClientFollowsPagesAndSkipsOutsideAndAddresslessEntries()
    {
        var people = await Client(new FakeGraph()).GetUsersAsync(CancellationToken.None);

        Assert.Equal(new[] { "1", "2" }, people.Select(p => p.Id));
        Assert.Equal(("Ada", "Kaya", "Satış", true), (people[0].FirstName, people[0].LastName, people[0].Department, people[0].IsEnabled));
        Assert.Equal(("Mehmet Ali", "Demir", false), (people[1].FirstName, people[1].LastName, people[1].IsEnabled));
    }

    [Fact]
    public async Task GraphErrorIsReportedAsUnavailable()
    {
        await Assert.ThrowsAsync<DirectoryUnavailableException>(() =>
            Client(new FakeGraph { Status = HttpStatusCode.Forbidden }).GetUsersAsync(CancellationToken.None));
    }

    [Fact]
    public void NameFallsBackToTheAddressWhenTheDirectoryHasNone()
    {
        Assert.Equal(("ada", "-"), DirectoryNames.Split(null, null, null, "ada@x.com"));
        Assert.Equal(("Ada", "-"), DirectoryNames.Split(null, null, "Ada", "ada@x.com"));
    }

    [Fact]
    public void LdapFilterEscapesEverySpecialCharacter()
    {
        Assert.Equal(@"\2a\28\29\5cx", LdapDirectoryClient.EscapeFilter(@"*()\x"));
    }

    [Theory]
    [InlineData("512", true)]
    [InlineData("514", false)]
    [InlineData(null, true)]
    public void LdapDisabledBitIsRead(string? flags, bool enabled)
    {
        Assert.Equal(enabled, LdapDirectoryClient.IsEnabled(flags));
    }

    [Fact]
    public async Task EmptyPasswordNeverReachesTheServer()
    {
        // The host does not exist, so reaching it would throw instead of answering false
        var client = new LdapDirectoryClient(Options.Create(new DirectoryOptions { Ldap = new LdapOptions { Host = "yok.invalid" } }));

        Assert.False(await client.CheckPasswordAsync("a@x.com", "", CancellationToken.None));
    }

    [Theory]
    [InlineData("common")]
    [InlineData("Organizations")]
    [InlineData("consumers")]
    public void SharedTenantsAreRefused(string tenant)
    {
        var options = new DirectoryOptions
        {
            Provider = DirectoryProvider.Entra,
            Entra = new EntraOptions { TenantId = tenant, ClientId = "c", ClientSecret = "s" }
        };

        Assert.Contains("own tenant", options.Validate(isDevelopment: true));
    }

    [Fact]
    public void LdapWithoutTlsIsAllowedOnlyInDevelopment()
    {
        var options = new DirectoryOptions
        {
            Provider = DirectoryProvider.ActiveDirectory,
            Ldap = new LdapOptions { Host = "dc", UseSsl = false, BindDn = "b", BindPassword = "p", BaseDn = "d" }
        };

        Assert.Null(options.Validate(isDevelopment: true));
        Assert.NotNull(options.Validate(isDevelopment: false));
    }

    [Fact]
    public void NoDirectoryNeedsNoSettings()
    {
        Assert.Null(new DirectoryOptions().Validate(isDevelopment: false));
    }
}
