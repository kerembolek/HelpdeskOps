using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

public class DashboardViewModel
{
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public int UnassignedActiveTickets { get; set; }

    // Active tickets past their SLA target, and the share of resolved tickets that met it (null before any is resolved)
    public int OverdueTickets { get; set; }
    public double? SlaComplianceRate { get; set; }

    // Devices still in use (not retired) whose warranty ends soon or has ended
    public int ExpiringWarrantyDevices { get; set; }
    public int ExpiredWarrantyDevices { get; set; }

    // Null when no ticket has been resolved yet
    public double? AverageResolutionMinutes { get; set; }

    public List<PriorityResolutionRow> ResolutionByPriority { get; set; } = new();
    public List<ProblemDeviceRow> ProblemDevices { get; set; } = new();
    public List<DepartmentWorkloadRow> DepartmentWorkload { get; set; } = new();
}

public record PriorityResolutionRow(TicketPriority Priority, int ResolvedCount, double AverageMinutes);

public record ProblemDeviceRow(string Brand, string Model, int TicketCount, int DeviceCount);

public record DepartmentWorkloadRow(string Department, int TotalTickets, int ActiveTickets);
