using System.Net;
using System.Text.Json;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Email;
using HelpdeskOps.Services.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class TeamsOptionsTests
{
    [Theory]
    [InlineData("", false, true)]
    [InlineData("https://prod.powerplatform.com/invoke?sig=abc", false, true)]
    [InlineData("http://prod.powerplatform.com/invoke?sig=abc", false, false)]
    [InlineData("http://prod.powerplatform.com/invoke?sig=abc", true, false)]
    [InlineData("http://localhost:9000/hook", true, true)]
    [InlineData("http://localhost:9000/hook", false, false)]
    [InlineData("not an address", false, false)]
    public void Validate(string url, bool isDevelopment, bool isValid)
    {
        var error = new TeamsOptions { WebhookUrl = url }.Validate(isDevelopment);

        Assert.Equal(isValid, error == null);
    }
}

public class TeamsCardTests
{
    private static readonly Ticket Ticket = new()
    {
        Id = 42, Title = "Ayşe Yılmaz maaş dosyası", Description = "T.C. 12345678901", RequesterId = "emp-1",
        Category = TicketCategory.Printer, Subcategory = TicketSubcategory.Other, OtherCategoryText = "Ayşe'nin yazıcısı",
        Priority = TicketPriority.High, SlaDueAt = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void CardHasTheShapeTheWebhookExpects()
    {
        using var json = JsonDocument.Parse(TeamsCard.Build(Ticket, "https://destek.test/Tickets/Details/42"));

        Assert.Equal("message", json.RootElement.GetProperty("type").GetString());
        var attachment = Assert.Single(json.RootElement.GetProperty("attachments").EnumerateArray());
        Assert.Equal("application/vnd.microsoft.card.adaptive", attachment.GetProperty("contentType").GetString());
        Assert.Equal(JsonValueKind.Null, attachment.GetProperty("contentUrl").ValueKind);

        var card = attachment.GetProperty("content");
        Assert.Equal("AdaptiveCard", card.GetProperty("type").GetString());
        Assert.Equal("Yeni talep #42", card.GetProperty("body")[0].GetProperty("text").GetString());
        var action = Assert.Single(card.GetProperty("actions").EnumerateArray());
        Assert.Equal("https://destek.test/Tickets/Details/42", action.GetProperty("url").GetString());
    }

    // The privacy notice promises personal data stays on the company's server
    [Fact]
    public void CardCarriesNoFreeTextOrPersonalData()
    {
        var card = TeamsCard.Build(Ticket, "https://destek.test/Tickets/Details/42");
        var readable = JsonDocument.Parse(card).RootElement.ToString();

        Assert.DoesNotContain("Yılmaz", readable);
        Assert.DoesNotContain("12345678901", readable);
        Assert.DoesNotContain("Ayşe", readable);
        Assert.DoesNotContain("emp-1", readable);
    }
}

public class TicketTeamsInterceptorTests : KvkkTestDatabase
{
    private readonly TeamsQueue _queue;

    public TicketTeamsInterceptorTests() : this(new TeamsQueue())
    {
    }

    private TicketTeamsInterceptorTests(TeamsQueue queue) : base(
        new SlaDueInterceptor(Options.Create(new SlaOptions())),
        new AuditInterceptor(new HttpContextAccessor()),
        new TicketTeamsInterceptor(queue, Options.Create(new TeamsOptions { WebhookUrl = "https://hook.test/invoke" }),
            Options.Create(new EmailOptions { PublicUrl = "https://destek.test/" }), new HttpContextAccessor()))
    {
        _queue = queue;
    }

    private Ticket NewTicket() => new()
    {
        Title = "Monitör", Description = "-", RequesterId = "emp-1", Category = TicketCategory.Hardware, Priority = TicketPriority.High
    };

    [Fact]
    public async Task NewTicketIsPostedWithItsIdAndSlaTarget()
    {
        var ticket = NewTicket();
        Context.Tickets.Add(ticket);
        await Context.SaveChangesAsync();

        var message = Assert.Single(await TeamsTestQueue.ReadAsync(_queue));
        Assert.Equal(ticket.Id, message.TicketId);
        Assert.NotEqual(0, ticket.Id);
        Assert.Contains($"https://destek.test/Tickets/Details/{ticket.Id}", message.Json);
        Assert.Contains(ticket.SlaDueAt!.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), message.Json);
    }

    [Fact]
    public async Task EditingATicketPostsNothing()
    {
        var ticket = NewTicket();
        Context.Tickets.Add(ticket);
        await Context.SaveChangesAsync();
        await TeamsTestQueue.ReadAsync(_queue);

        ticket.Status = TicketStatus.Resolved;
        await Context.SaveChangesAsync();

        Assert.Empty(await TeamsTestQueue.ReadAsync(_queue));
    }
}

public class TicketTeamsInterceptorOffTests : KvkkTestDatabase
{
    private readonly TeamsQueue _queue;

    public TicketTeamsInterceptorOffTests() : this(new TeamsQueue())
    {
    }

    private TicketTeamsInterceptorOffTests(TeamsQueue queue) : base(
        new TicketTeamsInterceptor(queue, Options.Create(new TeamsOptions()),
            Options.Create(new EmailOptions { PublicUrl = "https://destek.test" }), new HttpContextAccessor()))
    {
        _queue = queue;
    }

    [Fact]
    public async Task NothingIsPostedWithoutAWebhook()
    {
        Context.Tickets.Add(new Ticket { Title = "A", Description = "-", RequesterId = "emp-1" });
        await Context.SaveChangesAsync();

        Assert.Empty(await TeamsTestQueue.ReadAsync(_queue));
    }
}

public class TeamsDeliveryServiceTests
{
    private const string WebhookUrl = "https://hook.test/invoke?sig=secret";

    private sealed class FakeHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = new();

        public TaskCompletionSource SecondRequest { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, await request.Content!.ReadAsStringAsync(cancellationToken)));
            if (Requests.Count == 2)
            {
                SecondRequest.SetResult();
            }
            return respond(Requests.Count);
        }
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static TeamsDeliveryService Service(TeamsQueue queue, HttpMessageHandler handler) => new(queue,
        Options.Create(new TeamsOptions { WebhookUrl = WebhookUrl }), new FakeFactory(handler), NullLogger<TeamsDeliveryService>.Instance);

    [Fact]
    public async Task PostsTheCardAsJsonToTheWebhook()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));

        await Service(new TeamsQueue(), handler).DeliverAsync(new TeamsMessage(1, "{\"type\":\"message\"}"), CancellationToken.None);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(WebhookUrl, request.RequestUri!.ToString());
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"type\":\"message\"}", body);
    }

    [Fact]
    public async Task RefusedCardIsAnError()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Service(new TeamsQueue(), handler).DeliverAsync(new TeamsMessage(1, "{}"), CancellationToken.None));
    }

    // A timeout surfaces as a cancellation; it must not end the loop, or stop the whole app with it
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedCardDoesNotStopTheNextOne(bool timesOut)
    {
        var queue = new TeamsQueue();
        var handler = new FakeHandler(count => count == 1
            ? (timesOut ? throw new TaskCanceledException() : new HttpResponseMessage(HttpStatusCode.InternalServerError))
            : new HttpResponseMessage(HttpStatusCode.Accepted));
        var service = Service(queue, handler);
        queue.Enqueue(new TeamsMessage(1, "{}"));
        queue.Enqueue(new TeamsMessage(2, "{}"));

        await service.StartAsync(CancellationToken.None);
        await handler.SecondRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }
}

internal static class TeamsTestQueue
{
    public static async Task<List<TeamsMessage>> ReadAsync(TeamsQueue queue)
    {
        var messages = new List<TeamsMessage>();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        try
        {
            await foreach (var message in queue.ReadAllAsync(cancel.Token))
            {
                messages.Add(message);
            }
        }
        catch (OperationCanceledException)
        {
        }
        return messages;
    }
}
