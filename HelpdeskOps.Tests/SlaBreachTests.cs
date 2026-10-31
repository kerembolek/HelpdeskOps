using System.Text.Json;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class SlaBreachCardTests
{
    private static readonly Ticket Ticket = new()
    {
        Id = 42, Title = "Ayşe Yılmaz maaş dosyası", Description = "T.C. 12345678901", RequesterId = "emp-1",
        Category = TicketCategory.Printer, Priority = TicketPriority.High
    };

    [Fact]
    public void CardNamesTheBreachAndCarriesNoFreeText()
    {
        using var json = JsonDocument.Parse(TeamsCard.BuildSlaBreach(Ticket, "https://destek.test/Tickets/Details/42"));
        var readable = json.RootElement.ToString();

        var card = json.RootElement.GetProperty("attachments")[0].GetProperty("content");
        Assert.Equal("SLA hedefi aşıldı: talep #42", card.GetProperty("body")[0].GetProperty("text").GetString());
        Assert.DoesNotContain("Yılmaz", readable);
        Assert.DoesNotContain("12345678901", readable);
        Assert.DoesNotContain("emp-1", readable);
    }

    [Fact]
    public void NewTicketCardKeepsItsHeading()
    {
        using var json = JsonDocument.Parse(TeamsCard.Build(Ticket, "https://destek.test/Tickets/Details/42"));

        var card = json.RootElement.GetProperty("attachments")[0].GetProperty("content");
        Assert.Equal("Yeni talep #42", card.GetProperty("body")[0].GetProperty("text").GetString());
    }
}

public class SlaBreachNotifierTests : KvkkTestDatabase
{
    private const string PublicUrl = "https://destek.test/";
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly TeamsQueue _queue = new();

    public SlaBreachNotifierTests() : base(new SlaDueInterceptor(Options.Create(new SlaOptions())), new AuditInterceptor(new HttpContextAccessor()))
    {
    }

    private SlaBreachNotifier Notifier() => new(Context, _queue);

    // The target is set after the first save, because the interceptor works it out on every new ticket
    private async Task<Ticket> AddTicketAsync(TimeSpan dueIn, TicketStatus status = TicketStatus.Open)
    {
        var ticket = new Ticket
        {
            Title = "Monitör", Description = "-", RequesterId = "emp-1", Category = TicketCategory.Hardware,
            Priority = TicketPriority.High, Status = status
        };
        Context.Tickets.Add(ticket);
        await Context.SaveChangesAsync();
        await Context.Tickets.Where(t => t.Id == ticket.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.SlaDueAt, Now + dueIn));
        return ticket;
    }

    [Fact]
    public async Task AnOverdueTicketIsPostedWithItsLink()
    {
        var ticket = await AddTicketAsync(TimeSpan.FromMinutes(-1));

        Assert.Equal(1, await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None));

        var message = Assert.Single(await TeamsTestQueue.ReadAsync(_queue));
        Assert.Equal(ticket.Id, message.TicketId);
        Assert.Contains($"https://destek.test/Tickets/Details/{ticket.Id}", message.Json);
    }

    [Fact]
    public async Task EachBreachIsPostedOnce()
    {
        var ticket = await AddTicketAsync(TimeSpan.FromMinutes(-1));

        await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None);

        Assert.Equal(0, await Notifier().RunAsync(PublicUrl, Now.AddMinutes(5), CancellationToken.None));
        var stored = await Context.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticket.Id);
        Assert.Equal(Now, stored.SlaBreachNotifiedAt);
    }

    [Fact]
    public async Task ATicketStillOnTimeIsLeftAlone()
    {
        await AddTicketAsync(TimeSpan.FromMinutes(10));

        Assert.Equal(0, await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None));
    }

    [Fact]
    public async Task AResolvedTicketIsLeftAlone()
    {
        await AddTicketAsync(TimeSpan.FromMinutes(-1), TicketStatus.Resolved);

        Assert.Equal(0, await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None));
    }

    // The first run after an upgrade must not flood the channel with old tickets
    [Fact]
    public async Task AnOldBreachIsNotPosted()
    {
        await AddTicketAsync(TimeSpan.FromDays(-4));

        Assert.Equal(0, await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None));
    }

    [Fact]
    public async Task ABusyRunStopsAtItsLimitAndTheNextRunContinues()
    {
        for (var i = 0; i < 30; i++)
        {
            await AddTicketAsync(TimeSpan.FromMinutes(-1));
        }

        Assert.Equal(25, await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None));
        Assert.Equal(5, await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None));
    }

    [Fact]
    public async Task MarkingATicketLeavesNoAuditEntryOrUpdateTime()
    {
        var ticket = await AddTicketAsync(TimeSpan.FromMinutes(-1));
        var auditEntries = await Context.AuditLogs.CountAsync();

        await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None);

        Assert.Equal(auditEntries, await Context.AuditLogs.CountAsync());
        var stored = await Context.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticket.Id);
        Assert.Null(stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangingThePriorityAllowsANewBreachNotice()
    {
        var added = await AddTicketAsync(TimeSpan.FromMinutes(-1));
        await Notifier().RunAsync(PublicUrl, Now, CancellationToken.None);

        var ticket = await Context.Tickets.FirstAsync(t => t.Id == added.Id);
        await Context.Entry(ticket).ReloadAsync();
        ticket.Priority = TicketPriority.Low;
        await Context.SaveChangesAsync();

        var stored = await Context.Tickets.AsNoTracking().FirstAsync(t => t.Id == added.Id);
        Assert.Null(stored.SlaBreachNotifiedAt);
    }
}
