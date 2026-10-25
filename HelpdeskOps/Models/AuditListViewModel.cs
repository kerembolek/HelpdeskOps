using System.Text.Json;

namespace HelpdeskOps.Models;

public class AuditListViewModel
{
    public List<AuditLog> Logs { get; set; } = new();

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageCount { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? Query { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public static IReadOnlyList<AuditChange> ChangesOf(AuditLog log) =>
        string.IsNullOrEmpty(log.Changes)
            ? Array.Empty<AuditChange>()
            : JsonSerializer.Deserialize<List<AuditChange>>(log.Changes) ?? new List<AuditChange>();

    // Where the record can be opened, if it still has a page of its own
    public static string? LinkOf(AuditLog log) => log.Action == AuditAction.Deleted || log.EntityId == null
        ? null
        : log.EntityType switch
        {
            "Device" => $"/Devices/Details/{log.EntityId}",
            "Ticket" => $"/Tickets/Details/{log.EntityId}",
            "AssignmentForm" => $"/AssignmentForms/Pdf/{log.EntityId}",
            _ => null
        };
}
