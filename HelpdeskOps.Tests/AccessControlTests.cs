using System.Net;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace HelpdeskOps.Tests;

public class AccessControlTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;

    public AccessControlTests(TestApp app)
    {
        _app = app;
    }

    [Theory]
    [InlineData("/Devices")]
    [InlineData("/Tickets")]
    [InlineData("/MyDevices")]
    [InlineData("/Dashboard")]
    [InlineData("/Reports")]
    [InlineData("/Audit")]
    [InlineData("/Users")]
    [InlineData("/Licenses")]
    [InlineData("/Consumables")]
    [InlineData("/Checklists")]
    [InlineData("/Retention")]
    public async Task Anonymous_visitors_are_challenged(string path)
    {
        var response = await _app.ClientFor().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/Devices")]
    [InlineData("/Devices/Create")]
    [InlineData("/Dashboard")]
    [InlineData("/Reports")]
    [InlineData("/Audit")]
    [InlineData("/Users")]
    [InlineData("/Licenses")]
    [InlineData("/Consumables")]
    [InlineData("/Checklists")]
    [InlineData("/Retention")]
    public async Task Employees_cannot_open_it_pages(string path)
    {
        var response = await _app.ClientFor("emp-1", AppRoles.Employee).GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Devices")]
    [InlineData("/Audit")]
    [InlineData("/Users")]
    [InlineData("/Licenses")]
    [InlineData("/Consumables")]
    [InlineData("/Checklists")]
    [InlineData("/Retention")]
    public async Task Managers_cannot_open_it_management_pages(string path)
    {
        var response = await _app.ClientFor("mgr-1", AppRoles.Manager).GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Dashboard")]
    [InlineData("/Reports")]
    public async Task Managers_can_open_reports(string path)
    {
        var response = await _app.ClientFor("mgr-1", AppRoles.Manager).GetAsync(path);

        // Their queries use SQL Server's DATEDIFF, which SQLite cannot run, so only the access decision is checked
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Devices")]
    [InlineData("/Audit")]
    [InlineData("/Users")]
    [InlineData("/Licenses")]
    [InlineData("/Consumables")]
    [InlineData("/Checklists")]
    [InlineData("/Retention")]
    public async Task It_staff_can_open_it_pages(string path)
    {
        var response = await _app.ClientFor("it-1", AppRoles.ITAdmin).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Employees_can_open_their_own_ticket_but_not_someone_elses()
    {
        var ticketId = await AddTicketAsync("access-owner");

        var owner = await _app.ClientFor("access-owner", AppRoles.Employee).GetAsync($"/Tickets/Details/{ticketId}");
        var stranger = await _app.ClientFor("access-stranger", AppRoles.Employee).GetAsync($"/Tickets/Details/{ticketId}");

        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
    }

    private async Task<int> AddTicketAsync(string requesterId)
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var id in new[] { "access-owner", "access-stranger" })
        {
            if (await context.Users.FindAsync(id) == null)
            {
                context.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = id + "@example.com", FirstName = "Test", LastName = id });
            }
        }

        var ticket = new Ticket
        {
            Title = "Yazıcı", Description = "-", RequesterId = requesterId, Category = TicketCategory.Hardware,
            Priority = TicketPriority.Low
        };
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return ticket.Id;
    }
}
