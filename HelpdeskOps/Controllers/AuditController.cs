using HelpdeskOps;
using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Who changed what and when, for IT staff
[Authorize(Roles = "ITAdmin")]
public class AuditController : Controller
{
    public const int PageSize = 50;

    private readonly ApplicationDbContext _context;

    public AuditController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Audit?entityType=Device&entityId=5&q=&from=&to=&page=2
    public async Task<IActionResult> Index(string? entityType, string? entityId, string? q, DateTime? from, DateTime? to, int page = 1)
    {
        var logs = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(entityType) && AuditInterceptor.EntityNames.ContainsKey(entityType))
        {
            logs = logs.Where(a => a.EntityType == entityType);
        }
        else
        {
            entityType = null;
        }

        if (!string.IsNullOrEmpty(entityId))
        {
            logs = logs.Where(a => a.EntityId == entityId);
        }

        // Searches the person, the record label, and the changed values
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            logs = logs.Where(a => (a.UserName != null && a.UserName.Contains(term))
                || (a.EntityLabel != null && a.EntityLabel.Contains(term))
                || (a.Changes != null && a.Changes.Contains(term)));
        }

        // The dates are Turkey calendar days; the stored values are UTC
        if (from is DateTime fromDay)
        {
            var fromUtc = TurkeyTime.ToUtc(fromDay.Date);
            logs = logs.Where(a => a.CreatedAt >= fromUtc);
        }
        if (to is DateTime toDay)
        {
            var toUtc = TurkeyTime.ToUtc(toDay.Date.AddDays(1));
            logs = logs.Where(a => a.CreatedAt < toUtc);
        }

        var model = new AuditListViewModel
        {
            Logs = await PagedList<AuditLog>.CreateAsync(
                logs.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id), page, PageSize),
            EntityType = entityType,
            EntityId = entityId,
            Query = q,
            From = from,
            To = to
        };
        return View(model);
    }
}
