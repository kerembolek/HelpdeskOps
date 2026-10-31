using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services.Suggestions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

// Runs the real SDK client against a local stand-in for the Messages API, so the request and the failure paths are real
public class AnthropicTicketSuggesterTests : IDisposable
{
    private const string ValidAnswer = """{"category":"Printer","subcategory":"NotPrinting","priority":"High","reason":"Yazıcı sorunu.","articleIds":[7]}""";

    private static readonly List<SuggestionArticle> Articles = [new(7, "Yazıcı kuyruğu", TicketCategory.Printer, null)];

    private readonly HttpListener _listener = new();
    private readonly int _port = FreePort();
    private readonly List<string> _requests = [];
    private readonly CapturingLogger _logger = new();

    private int _status = 200;
    private string _stopReason = "end_turn";
    private string _answer = ValidAnswer;
    private TimeSpan _delay = TimeSpan.Zero;

    public AnthropicTicketSuggesterTests()
    {
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        _ = ServeAsync();
    }

    public void Dispose() => _listener.Close();

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task ServeAsync()
    {
        try
        {
            while (true)
            {
                var context = await _listener.GetContextAsync();
                _ = RespondAsync(context);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException)
        {
            // The test ended and closed the listener
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            lock (_requests)
            {
                _requests.Add(body);
            }

            await Task.Delay(_delay);
            var message = JsonSerializer.Serialize(new
            {
                id = "msg_test", type = "message", role = "assistant", model = "claude-opus-5-5",
                content = new[] { new { type = "text", text = _answer } },
                stop_reason = _stopReason, stop_sequence = (string?)null,
                usage = new { input_tokens = 10, output_tokens = 10 }
            });
            var payload = Encoding.UTF8.GetBytes(_status == 200 ? message : """{"type":"error","error":{"type":"api_error","message":"failed"}}""");
            context.Response.StatusCode = _status;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(payload);
            context.Response.Close();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException or InvalidOperationException)
        {
            // The client timed out and left, or the test ended
        }
    }

    private AnthropicTicketSuggester Suggester(int? port = null, int timeoutSeconds = 20) => new(
        Options.Create(new SuggestionsOptions
        {
            ApiKey = "test-key", BaseUrl = $"http://localhost:{port ?? _port}", TimeoutSeconds = timeoutSeconds
        }), _logger);

    private Task<TicketSuggestion?> SuggestAsync(AnthropicTicketSuggester suggester, CancellationToken cancellationToken = default) =>
        suggester.SuggestAsync("Yazıcı bozuk", "Ulaşın: ali@sirket.com.tr", Articles, cancellationToken);

    [Fact]
    public async Task AValidAnswerBecomesASuggestion()
    {
        var suggestion = await SuggestAsync(Suggester());

        Assert.NotNull(suggestion);
        Assert.Equal(TicketCategory.Printer, suggestion.Category);
        Assert.Equal(7, Assert.Single(suggestion.Articles).Id);
    }

    [Fact]
    public async Task TheRequestIsMaskedAndAsksForTheSchema()
    {
        await SuggestAsync(Suggester());

        var body = Assert.Single(_requests);
        Assert.DoesNotContain("ali@sirket.com.tr", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("claude-opus-5-5", json.RootElement.GetProperty("model").GetString());
        var config = json.RootElement.GetProperty("output_config");
        Assert.Equal("low", config.GetProperty("effort").GetString());
        Assert.Equal("json_schema", config.GetProperty("format").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("refusal")]
    [InlineData("max_tokens")]
    public async Task AnAnswerThatDidNotFinishIsDropped(string stopReason)
    {
        _stopReason = stopReason;

        Assert.Null(await SuggestAsync(Suggester()));
    }

    [Fact]
    public async Task AnAnswerThatFailsValidationIsDropped()
    {
        _answer = """{"category":"Printer","subcategory":"Vpn","priority":"High","reason":"-","articleIds":[]}""";

        Assert.Null(await SuggestAsync(Suggester()));
    }

    [Fact]
    public async Task ARejectedKeyIsNotRetried()
    {
        _status = 401;

        Assert.Null(await SuggestAsync(Suggester()));
        Assert.Single(_requests);
    }

    [Fact]
    public async Task AServerErrorIsRetriedOnceThenDropped()
    {
        _status = 500;

        Assert.Null(await SuggestAsync(Suggester()));
        Assert.Equal(2, _requests.Count);
    }

    [Fact]
    public async Task AnUnreachableServiceIsDropped()
    {
        Assert.Null(await SuggestAsync(Suggester(port: FreePort())));
    }

    // A request timeout is also a cancellation, and must not look like the person leaving the page
    [Fact]
    public async Task ATimeoutIsDropped()
    {
        _delay = TimeSpan.FromSeconds(5);

        Assert.Null(await SuggestAsync(Suggester(timeoutSeconds: 1)));
    }

    [Fact]
    public async Task LeavingThePageCancelsTheRequest()
    {
        _delay = TimeSpan.FromSeconds(5);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SuggestAsync(Suggester(), cancellation.Token));
    }

    [Fact]
    public async Task TheLogNeverCarriesTheTicketTextOrTheKey()
    {
        _status = 500;

        await SuggestAsync(Suggester());

        var log = string.Join("\n", _logger.Entries);
        Assert.Contains("Could not get a ticket suggestion", log);
        Assert.DoesNotContain("Yazıcı bozuk", log);
        Assert.DoesNotContain("test-key", log);
    }

    private sealed class CapturingLogger : ILogger<AnthropicTicketSuggester>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add($"{formatter(state, exception)} {exception}");
    }
}
