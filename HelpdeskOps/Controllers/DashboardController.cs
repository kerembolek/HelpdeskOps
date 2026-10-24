using HelpdeskOps;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Ticket and device reports for IT staff and managers
[Authorize(Roles = "ITAdmin,Manager")]
public class DashboardController : Controller
{
    private const int ProblemDeviceLimit = 5;

    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Dashboard
    public async Task<IActionResult> Index()
    {
        var tickets = _context.Tickets.AsNoTracking();
        var resolved = tickets.Where(t => t.ResolvedAt != null);

        var statusCounts = await tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        int CountOf(TicketStatus status) => statusCounts.GetValueOrDefault(status);

        var resolvedCount = await resolved.CountAsync();
        var metTargetCount = await tickets.CountAsync(TicketSla.MetTarget());

        var devicesInUse =_context.Devices.AsNoTracking().Where(d => d.Status != DeviceStatus.Retired);
        var today = TurkeyTime.Today;
        var soon = today.AddDays(Device.WarrantyExpiringSoonDays);

        var model = new DashboardViewModel
        {
            TotalTickets = statusCounts.Values.Sum(),
            OpenTickets = CountOf(TicketStatus.Open),
            InProgressTickets = CountOf(TicketStatus.InProgress),
            ResolvedTickets = CountOf(TicketStatus.Resolved) + CountOf(TicketStatus.Closed),

            UnassignedActiveTickets = await tickets.CountAsync(t =>
                t.AssignedToId == null && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)),

            OverdueTickets = await tickets.CountAsync(TicketSla.IsOverdue(DateTime.UtcNow)),
            SlaComplianceRate = resolvedCount == 0 ? null : metTargetCount * 100.0 / resolvedCount,

            // Same rules as the warranty filters on the device list
            ExpiringWarrantyDevices = await devicesInUse.CountAsync(d => d.WarrantyEndDate >= today && d.WarrantyEndDate <= soon),
            ExpiredWarrantyDevices = await devicesInUse.CountAsync(d => d.WarrantyEndDate < today),

            AverageResolutionMinutes = await resolved
                .Select(t => EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt))
                .AverageAsync(),

            ResolutionByPriority = await resolved
                .GroupBy(t => t.Priority)
                .OrderByDescending(g => g.Key)
                .Select(g => new PriorityResolutionRow(
                    g.Key,
                    g.Count(),
                    g.Average(t => (double)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value))))
                .ToListAsync(),

            // Grouped by brand/model so repeated failures of the same hardware line stand out
            ProblemDevices = await tickets
                .Where(t => t.DeviceId != null)
                .GroupBy(t => new { t.Device!.Brand, t.Device.Model })
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key.Brand)
                .Take(ProblemDeviceLimit)
                .Select(g => new ProblemDeviceRow(
                    g.Key.Brand ?? "-",
                    g.Key.Model ?? "-",
                    g.Count(),
                    g.Select(t => t.DeviceId).Distinct().Count()))
                .ToListAsync(),

            DepartmentWorkload = await tickets
                .GroupBy(t => t.Requester!.Department ?? "Belirtilmemiş")
                .OrderByDescending(g => g.Count())
                .Select(g => new DepartmentWorkloadRow(
                    g.Key,
                    g.Count(),
                    g.Count(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)))
                .ToListAsync()
        };

        return View(model);
    }
}
