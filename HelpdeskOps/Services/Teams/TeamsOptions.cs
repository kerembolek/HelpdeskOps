namespace HelpdeskOps.Services.Teams;

// The "Teams" section of appsettings. The webhook address carries its own signature, so anyone holding it can post to
// the channel: it belongs in a git-ignored settings file, environment variables, or user secrets, and never in a log.
public class TeamsOptions
{
    public const string SectionName = "Teams";

    // From the Teams Workflows app, template "Send webhook alerts to a channel"; empty turns the feature off
    public string WebhookUrl { get; set; } = "";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(WebhookUrl);

    // Returns what is wrong with the settings, or null when they are usable, so a bad install stops at startup
    public string? Validate(bool isDevelopment)
    {
        if (!IsEnabled)
        {
            return null;
        }

        if (!Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var url))
        {
            return "Teams:WebhookUrl is not a valid address.";
        }

        // Plain http would expose the signature in the address; only a local test receiver may use it
        return url.Scheme == Uri.UriSchemeHttps || (isDevelopment && url.IsLoopback)
            ? null
            : "Teams:WebhookUrl must start with https://.";
    }
}
