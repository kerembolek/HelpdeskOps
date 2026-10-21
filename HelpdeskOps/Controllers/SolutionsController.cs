using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Tier 0 solution bank: every signed-in user can browse published articles; IT staff manage them
[Authorize]
public class SolutionsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SolutionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Solutions?category=Printer
    // Without a category the page shows the category picker
    public async Task<IActionResult> Index(TicketCategory? category)
    {
        ViewBag.Category = category;
        ViewBag.ArticleCounts = await _context.SolutionArticles
            .Where(a => a.IsPublished)
            .GroupBy(a => a.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        if (category == null)
        {
            return View(new List<SolutionArticle>());
        }

        return View(await _context.SolutionArticles
            .Where(a => a.IsPublished && a.Category == category)
            .OrderByDescending(a => a.SolvedCount)
            .ThenBy(a => a.Title)
            .ToListAsync());
    }

    // POST: Solutions/Solved/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Solved(int id)
    {
        var updated = await _context.SolutionArticles
            .Where(a => a.Id == id && a.IsPublished)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.SolvedCount, a => a.SolvedCount + 1));
        if (updated == 0)
        {
            return NotFound();
        }

        TempData["SolutionMessage"] = "Sorununuzun çözülmesine sevindik. Talep açmanıza gerek kalmadı.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Solutions/Escalate/5
    // Counts the article as not enough and opens the ticket form with its category and title filled in
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Escalate(int id)
    {
        var article = await _context.SolutionArticles.FirstOrDefaultAsync(a => a.Id == id && a.IsPublished);
        if (article == null)
        {
            return NotFound();
        }

        article.EscalatedCount++;
        await _context.SaveChangesAsync();
        return RedirectToAction("Create", "Tickets", new { category = article.Category, solutionId = article.Id });
    }

    // GET: Solutions/Manage
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Manage()
    {
        return View(await _context.SolutionArticles
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Title)
            .ToListAsync());
    }

    // GET: Solutions/Create
    [Authorize(Roles = "ITAdmin")]
    public IActionResult Create()
    {
        return View();
    }

    // POST: Solutions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Create([Bind("Title,Category,Symptoms,Steps,IsPublished")] SolutionArticle article)
    {
        if (ModelState.IsValid)
        {
            // Counters and timestamps are set by the server, not by the form
            article.SolvedCount = 0;
            article.EscalatedCount = 0;
            article.CreatedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;

            _context.Add(article);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Manage));
        }
        return View(article);
    }

    // GET: Solutions/Edit/5
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var article = await _context.SolutionArticles.FindAsync(id);
        return article == null ? NotFound() : View(article);
    }

    // POST: Solutions/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Category,Symptoms,Steps,IsPublished")] SolutionArticle article)
    {
        if (id != article.Id)
        {
            return NotFound();
        }

        var existing = await _context.SolutionArticles.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            // Only form fields are copied; counters and CreatedAt stay as stored
            existing.Title = article.Title;
            existing.Category = article.Category;
            existing.Symptoms = article.Symptoms;
            existing.Steps = article.Steps;
            existing.IsPublished = article.IsPublished;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Manage));
        }
        return View(article);
    }

    // GET: Solutions/Delete/5
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _context.SolutionArticles.FindAsync(id);
        return article == null ? NotFound() : View(article);
    }

    // POST: Solutions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ITAdmin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var article = await _context.SolutionArticles.FindAsync(id);
        if (article != null)
        {
            _context.SolutionArticles.Remove(article);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Manage));
    }
}
