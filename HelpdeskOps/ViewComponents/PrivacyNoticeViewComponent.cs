using System.Security.Claims;
using HelpdeskOps.Data;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Kvkk;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.ViewComponents;

// Asks a signed-in user to read the privacy notice until they confirm its current version
public class PrivacyNoticeViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly KvkkOptions _kvkk;
    private readonly IConfiguration _configuration;

    public PrivacyNoticeViewComponent(ApplicationDbContext context, IOptions<KvkkOptions> kvkk, IConfiguration configuration)
    {
        _context = context;
        _kvkk = kvkk.Value;
        _configuration = configuration;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        // The read-only demo cannot save the confirmation, and the notice page shows its own prompt
        if (userId == null || DemoReadOnlyMiddleware.IsEnabled(_configuration)
            || string.Equals(ViewContext.RouteData.Values["action"] as string, "Privacy", StringComparison.OrdinalIgnoreCase))
        {
            return Content(string.Empty);
        }

        var read = await _context.Users.AnyAsync(u => u.Id == userId && u.PrivacyNoticeVersion == _kvkk.NoticeVersion);
        return read ? Content(string.Empty) : View();
    }
}
