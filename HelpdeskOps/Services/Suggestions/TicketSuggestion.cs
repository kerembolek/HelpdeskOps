using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Services.Suggestions;

public record SuggestionArticle(int Id, string Title, TicketCategory Category, string? Symptoms);

public record TicketSuggestion(TicketCategory Category, TicketSubcategory Subcategory, TicketPriority Priority,
    string Reason, IReadOnlyList<SuggestionArticle> Articles);

// The form stays usable when this returns null: the person just picks the fields themselves
public interface ITicketSuggester
{
    Task<TicketSuggestion?> SuggestAsync(string title, string description, IReadOnlyList<SuggestionArticle> articles,
        CancellationToken cancellationToken);
}
