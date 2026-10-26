using HelpdeskOps.Services.Kvkk;

namespace HelpdeskOps.Models;

// The "Saklama Süreleri" page: each rule with how many records it would change right now, and the recent runs
public class RetentionViewModel
{
    public required IReadOnlyList<(RetentionRule Rule, int Due)> Rules { get; init; }

    public required List<AuditLog> RecentRuns { get; init; }
}
