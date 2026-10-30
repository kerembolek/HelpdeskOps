using System.Text.Json.Nodes;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Services.Teams;

// Builds the Adaptive Card the Teams webhook expects.
// Teams stores messages on Microsoft's servers, while the privacy notice promises that personal data stays on the
// company's own server. So the card carries only the number, fixed lists, and the target. Title, description, requester,
// and the free-text "Other" category stay behind the sign-in, and the button leads there (KVKK).
public static class TeamsCard
{
    public static string Build(Ticket ticket, string link)
    {
        var card = new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.2",
            ["body"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = $"Yeni talep #{ticket.Id}",
                    ["weight"] = "Bolder",
                    ["size"] = "Medium"
                },
                new JsonObject
                {
                    ["type"] = "FactSet",
                    ["facts"] = new JsonArray(
                        Fact("Kategori", ticket.Category.GetDisplayName()),
                        Fact("Öncelik", ticket.Priority.GetDisplayName()),
                        Fact("SLA Hedefi", ticket.SlaDueAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "-"))
                }),
            ["actions"] = new JsonArray(new JsonObject
            {
                ["type"] = "Action.OpenUrl",
                ["title"] = "HelpdeskOps'ta Görüntüle",
                ["url"] = link
            })
        };

        return new JsonObject
        {
            ["type"] = "message",
            ["attachments"] = new JsonArray(new JsonObject
            {
                ["contentType"] = "application/vnd.microsoft.card.adaptive",
                ["contentUrl"] = null,
                ["content"] = card
            })
        }.ToJsonString();
    }

    private static JsonObject Fact(string title, string value) => new() { ["title"] = title, ["value"] = value };
}
