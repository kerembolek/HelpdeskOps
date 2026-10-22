using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

// PDF copies of zimmet forms
[Authorize]
public class AssignmentFormsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AssignmentFormsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: AssignmentForms/Pdf/5
    // IT staff can download any form; everyone else only the forms issued to them
    public async Task<IActionResult> Pdf(int id)
    {
        var form = await _context.AssignmentForms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (form == null)
        {
            return NotFound();
        }

        if (!User.IsInRole("ITAdmin") && form.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return Forbid();
        }

        var pdf = new AssignmentFormDocument(form).GeneratePdf();
        return File(pdf, "application/pdf", $"{form.FormNumber}.pdf");
    }
}
