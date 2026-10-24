using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace HelpdeskOps.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public HomeController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
        }

        // Shortcut tiles with live counts; visitors who are not signed in see the sign-in options instead
        public async Task<IActionResult> Index()
        {
            var model = new HomeViewModel
            {
                ShowDemoAccounts = _environment.IsDevelopment() || _configuration.GetValue<bool>("SeedDemoData")
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return View(model);
            }

            var isAdmin = User.IsInRole("ITAdmin");
            var isManager = User.IsInRole("Manager");
            model.FirstName = await _context.Users.Where(u => u.Id == userId).Select(u => u.FirstName).FirstOrDefaultAsync();

            var myActiveTickets = await _context.Tickets.CountAsync(t => t.RequesterId == userId
                && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));
            var myDevices = await _context.Devices.CountAsync(d => d.AssignedUserId == userId);
            var formsToSign = await _context.AssignmentForms.CountAsync(f => f.UserId == userId
                && f.AcceptedAt == null && f.ReturnedAt == null);

            // IT opens tickets directly; everyone else goes through the self-service solutions first
            model.PersonalTiles.Add(isAdmin
                ? new HomeTile("Yeni Talep", "Bir çalışan adına ya da kendiniz için talep açın.", "plus-circle", "primary", Url.Action("Create", "Tickets")!)
                : new HomeTile("Yeni Talep", "Önce çözüm önerilerine bakın, gerekirse talep açın.", "plus-circle", "primary", Url.Action("Index", "Solutions")!));
            if (!isAdmin && !isManager)
            {
                model.PersonalTiles.Add(new HomeTile("Taleplerim", "Açık ve işlemdeki talepleriniz.", "ticket-detailed", "info",
                    Url.Action("Index", "Tickets", new { scope = "active" })!, myActiveTickets));
            }
            model.PersonalTiles.Add(new HomeTile("Cihazlarım", "Size zimmetli cihazlar ve zimmet formlarınız.", "laptop", "success",
                Url.Action("Index", "MyDevices")!, myDevices));
            if (formsToSign > 0)
            {
                model.PersonalTiles.Add(new HomeTile("İmza Bekleyen Formlar", "Teslim aldığınız cihazların formlarını imzalayın.", "pen", "warning",
                    Url.Action("Index", "MyDevices") + "#assignment-forms", formsToSign));
            }
            model.PersonalTiles.Add(new HomeTile("Kendin Çöz", "Sık karşılaşılan sorunların adım adım çözümleri.", "lightbulb", "secondary",
                Url.Action("Index", "Solutions")!));

            if (isAdmin || isManager)
            {
                var unassigned = await _context.Tickets.CountAsync(t => t.AssignedToId == null
                    && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));
                var overdue = await _context.Tickets.CountAsync(TicketSla.IsOverdue(DateTime.UtcNow));
                var active = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);

                if (isAdmin)
                {
                    var mine = await _context.Tickets.CountAsync(t => t.AssignedToId == userId
                        && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));
                    model.TeamTiles.Add(new HomeTile("Bana Atanan", "Üzerinizdeki aktif talepler.", "person-check", "primary",
                        Url.Action("Index", "Tickets", new { scope = "mine" })!, mine));
                }
                model.TeamTiles.Add(new HomeTile("Atanmamış Talepler", "Henüz kimsenin üstlenmediği aktif talepler.", "inbox", unassigned > 0 ? "warning" : "secondary",
                    Url.Action("Index", "Tickets", new { scope = "unassigned" })!, unassigned));
                model.TeamTiles.Add(new HomeTile("Hedef Süresi Aşan", "Çözüm hedefini geçmiş aktif talepler.", "alarm", overdue > 0 ? "danger" : "secondary",
                    Url.Action("Index", "Tickets", new { scope = "overdue" })!, overdue));
                model.TeamTiles.Add(new HomeTile("Tüm Aktif Talepler", "Açık ve işlemdeki tüm talepler.", "ticket-detailed", "info",
                    Url.Action("Index", "Tickets", new { scope = "active" })!, active));

                if (isAdmin)
                {
                    var pendingReturns = await _context.AssignmentForms.CountAsync(f => f.ReturnRequestedAt != null && f.ReturnedAt == null);
                    var today = TurkeyTime.Today;
                    var soon = today.AddDays(Device.WarrantyExpiringSoonDays);
                    var expiring = await _context.Devices.CountAsync(d => d.Status != DeviceStatus.Retired
                        && d.WarrantyEndDate >= today && d.WarrantyEndDate <= soon);

                    model.TeamTiles.Add(new HomeTile("İade Bekleyen Cihazlar", "Kontrol edip teslim almanız gereken cihazlar.", "box-arrow-in-down", pendingReturns > 0 ? "warning" : "secondary",
                        Url.Action("Index", "Devices")!, pendingReturns));
                    model.TeamTiles.Add(new HomeTile("Garantisi Bitecekler", $"{Device.WarrantyExpiringSoonDays} gün içinde garantisi biten cihazlar.", "shield-exclamation", expiring > 0 ? "warning" : "secondary",
                        Url.Action("Index", "Devices", new { warranty = "expiring" })!, expiring));
                }

                model.TeamTiles.Add(new HomeTile("Pano", "Çözüm süreleri, iş yükü ve sorunlu cihazlar.", "speedometer2", "primary", Url.Action("Index", "Dashboard")!));
                model.TeamTiles.Add(new HomeTile("Raporlar", "Döneme göre talep rakamları ve CSV dışa aktarma.", "bar-chart-line", "primary", Url.Action("Index", "Reports")!));
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
