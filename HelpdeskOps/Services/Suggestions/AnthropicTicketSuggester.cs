using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.Suggestions;

// Asks Claude for a suggestion. A failure is logged and returns null, because a person waiting on a form must never
// see an error page over an optional helper. The log never carries the ticket text or the key.
public class AnthropicTicketSuggester : ITicketSuggester
{
    private readonly SuggestionsOptions _options;
    private readonly ILogger<AnthropicTicketSuggester> _logger;
    private readonly AnthropicClient _client;

    public AnthropicTicketSuggester(IOptions<SuggestionsOptions> options, ILogger<AnthropicTicketSuggester> logger)
    {
        _options = options.Value;
        _logger = logger;
        var timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _client = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? new AnthropicClient { ApiKey = _options.ApiKey, Timeout = timeout, MaxRetries = 1 }
            : new AnthropicClient { ApiKey = _options.ApiKey, Timeout = timeout, MaxRetries = 1, BaseUrl = _options.BaseUrl };
    }

    public async Task<TicketSuggestion?> SuggestAsync(string title, string description,
        IReadOnlyList<SuggestionArticle> articles, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = 2048,
                System = TicketSuggestionPrompt.SystemPrompt,
                // Sorting one short ticket needs no deep thought, and every token is paid for
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = new JsonOutputFormat { Schema = TicketSuggestionPrompt.OutputSchema() }
                },
                Messages =
                [
                    new UserMessageParam { Content = TicketSuggestionPrompt.BuildUserMessage(title, description, articles) }
                ]
            }, cancellationToken);

            var answer = response.Content.Select(block => block.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
            if (response.StopReason != StopReason.EndTurn || answer == null)
            {
                _logger.LogWarning("Ticket suggestion stopped with reason {StopReason}", response.StopReason);
                return null;
            }

            var suggestion = TicketSuggestionPrompt.ParseAnswer(answer, articles);
            if (suggestion == null)
            {
                _logger.LogWarning("Ticket suggestion answer did not pass validation");
            }
            return suggestion;
        }
        // A request timeout is also a cancellation; only the person leaving the page counts as giving up
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Could not get a ticket suggestion");
            return null;
        }
    }
}
