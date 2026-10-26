using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Services.Kvkk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// KVKK retention periods: the rules, what is due now, and the recent runs
[Authorize(Roles = "ITAdmin")]
public class RetentionController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RetentionCleaner _cleaner;
    private readonly RetentionOptions _options;

    public RetentionController(ApplicationDbContext context, RetentionCleaner cleaner, IOptions<KvkkOptions> kvkk)
    {
        _context = context;
        _cleaner = cleaner;
        _options = kvkk.Value.Retention;
    }

    // GET: Retention
    public async Task<IActionResult> Index()
    {
        var due = await _cleaner.CountDueAsync(DateTime.UtcNow);
        var model = new RetentionViewModel
        {
            Rules = RetentionCleaner.Rules(_options).Zip(due.InRuleOrder()).ToList(),
            RecentRuns = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.EntityType == RetentionCleaner.AuditEntityType)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync()
        };
        return View(model);
    }

    // POST: Retention/Run
    // Applies the rules now instead of waiting for the daily run
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run()
    {
        var counts = await _cleaner.RunAsync(DateTime.UtcNow, User, HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["RetentionMessage"] = counts.Total == 0
            ? "Süresi dolan kişisel veri yok; hiçbir şey değişmedi."
            : $"Saklama süreleri uygulandı: {counts.Total} kayıt silindi veya anonimleştirildi.";
        return RedirectToAction(nameof(Index));
    }
}
