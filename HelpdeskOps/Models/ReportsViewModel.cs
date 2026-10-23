using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

public class ReportsViewModel
{
    // The period covers the last Days days, starting at From (UTC)
    public int Days { get; set; }
    public DateTime From { get; set; }

    public int OpenedTickets { get; set; }
    public int ResolvedTickets { get; set; }
    // Tickets opened in the period that are still open or in progress
    public int StillActiveTickets { get; set; }

    // Null when nothing was resolved in the period
    public double? AverageResolutionMinutes { get; set; }

    // Satisfaction ratings given in the period; the average is null when there are none
    public int RatedTickets { get; set; }
    public double? AverageSatisfaction { get; set; }

    public List<CategoryReportRow> Categories { get; set; } = new();
    public List<StaffReportRow> Staff { get; set; } = new();
}

public record CategoryReportRow(TicketCategory Category, TicketSubcategory? Subcategory, int TicketCount, int ResolvedCount, double? AverageMinutes);

public record StaffReportRow(string Name, int ActiveTickets, int ResolvedTickets, double? AverageMinutes, double? AverageRating);
