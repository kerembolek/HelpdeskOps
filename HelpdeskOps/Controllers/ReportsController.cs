using System.Text;
using HelpdeskOps;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Period-based management reports and a CSV export of the period's tickets
[Authorize(Roles = "ITAdmin,Manager")]
public class ReportsController : Controller
{
    private static readonly int[] Periods = { 7, 30, 90, 365 };
    private const int DefaultPeriod = 30;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // Anything other than the offered periods falls back to the default
    private static int NormalizeDays(int? days) => days is int d && Periods.Contains(d) ? d : DefaultPeriod;

    // GET: Reports?days=30
    public async Task<IActionResult> Index(int? days)
    {
        var period = NormalizeDays(days);
        var from = DateTime.UtcNow.AddDays(-period);

        var tickets = _context.Tickets.AsNoTracking();
        var opened = tickets.Where(t => t.CreatedAt >= from);
        var resolved = tickets.Where(t => t.ResolvedAt >= from);

        var staffStats = await tickets
            .Where(t => t.AssignedToId != null)
            .GroupBy(t => t.AssignedToId!)
            .Select(g => new
            {
                UserId = g.Key,
                Active = g.Count(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress),
                Resolved = g.Count(t => t.ResolvedAt >= from),
                AverageMinutes = g.Average(t => t.ResolvedAt >= from
                    ? (double?)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value)
                    : null),
                // Ratings given in the period for tickets this person handled
                AverageRating = g.Average(t => t.RatedAt >= from ? (double?)t.SatisfactionRating : null)
            })
            .ToDictionaryAsync(x => x.UserId);

        // Every IT staff member is listed, including those without tickets in the period
        var staff = (await _userManager.GetUsersInRoleAsync("ITAdmin"))
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => staffStats.TryGetValue(u.Id, out var s)
                ? new StaffReportRow($"{u.FirstName} {u.LastName}", s.Active, s.Resolved, s.AverageMinutes, s.AverageRating)
                : new StaffReportRow($"{u.FirstName} {u.LastName}", 0, 0, null, null))
            .ToList();

        var model = new ReportsViewModel
        {
            Days = period,
            From = from,
            OpenedTickets = await opened.CountAsync(),
            ResolvedTickets = await resolved.CountAsync(),
            StillActiveTickets = await opened.CountAsync(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress),

            RatedTickets = await tickets.CountAsync(t => t.RatedAt >= from),
            AverageSatisfaction = await tickets
                .Where(t => t.RatedAt >= from)
                .AverageAsync(t => (double?)t.SatisfactionRating),

            AverageResolutionMinutes = await resolved
                .Select(t => EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt))
                .AverageAsync(),

            Categories = await opened
                .GroupBy(t => new { t.Category, t.Subcategory })
                .OrderBy(g => g.Key.Category)
                .ThenByDescending(g => g.Count())
                .Select(g => new CategoryReportRow(
                    g.Key.Category,
                    g.Key.Subcategory,
                    g.Count(),
                    g.Count(t => t.ResolvedAt != null),
                    g.Average(t => t.ResolvedAt != null
                        ? (double?)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value)
                        : null)))
                .ToListAsync(),

            Staff = staff
        };

        return View(model);
    }

    // GET: Reports/Export?days=30
    // Semicolon-separated with a UTF-8 BOM so Excel with Turkish settings opens it with the right columns and letters
    public async Task<IActionResult> Export(int? days)
    {
        var period = NormalizeDays(days);
        var from = DateTime.UtcNow.AddDays(-period);

        var tickets = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Requester)
            .Include(t => t.AssignedTo)
            .Where(t => t.CreatedAt >= from)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        static string Cell(string? value)
        {
            value ??= "";
            // Titles are typed by users; a leading = + - @ would run as a formula when opened in Excel
            if (value.Length > 0 && "=+-@".Contains(value[0]))
            {
                value = "'" + value;
            }
            return value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }

        static string Name(ApplicationUser? user) => user != null ? $"{user.FirstName} {user.LastName}" : "";

        var csv = new StringBuilder();
        csv.AppendLine("No;Başlık;Kategori;Öncelik;Durum;Talep Eden;Departman;Atanan Kişi;Oluşturulma;Çözülme;Çözüm Süresi (saat);Memnuniyet (1-5)");
        foreach (var t in tickets)
        {
            var hours = t.ResolvedAt is DateTime resolvedAt ? (resolvedAt - t.CreatedAt).TotalHours.ToString("0.0") : "";
            csv.AppendLine(string.Join(";",
                t.Id.ToString(),
                Cell(t.Title),
                Cell(t.CategoryPath),
                Cell(t.Priority.GetDisplayName()),
                Cell(t.Status.GetDisplayName()),
                Cell(Name(t.Requester)),
                Cell(t.Requester?.Department),
                Cell(Name(t.AssignedTo)),
                t.CreatedAt.ToTurkeyTime().ToString("dd.MM.yyyy HH:mm"),
                t.ResolvedAt?.ToTurkeyTime().ToString("dd.MM.yyyy HH:mm") ?? "",
                hours,
                t.SatisfactionRating?.ToString() ?? ""));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"talepler-son-{period}-gun-{TurkeyTime.Now:yyyyMMdd}.csv");
    }
}
