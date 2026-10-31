namespace HelpdeskOps.Services.Suggestions;

// The "Suggestions" section of appsettings. The API key spends the company's money and opens a path for ticket text to
// leave the server: it belongs in a git-ignored settings file, environment variables, or user secrets, and never in a log.
public class SuggestionsOptions
{
    public const string SectionName = "Suggestions";

    // From the Claude Console; empty turns the feature off, which is the default
    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "claude-opus-5-5";

    // Only for pointing at a local test server; empty uses Anthropic's own address
    public string BaseUrl { get; set; } = "";

    // How long a person waits on the form for one attempt
    public int TimeoutSeconds { get; set; } = 20;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);

    // Returns what is wrong with the settings, or null when they are usable, so a bad install stops at startup
    public string? Validate(bool isDevelopment)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(BaseUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var url))
        {
            return "Suggestions:BaseUrl is not a valid address.";
        }

        // Plain http would expose the key and the ticket text; only a local test server may use it
        return url.Scheme == Uri.UriSchemeHttps || (isDevelopment && url.IsLoopback)
            ? null
            : "Suggestions:BaseUrl must start with https://.";
    }
}
