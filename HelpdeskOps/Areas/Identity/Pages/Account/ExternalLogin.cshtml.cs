// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HelpdeskOps.Models;
using HelpdeskOps.Services.CompanyDirectory;

namespace HelpdeskOps.Areas.Identity.Pages.Account;

// Sign-in through Microsoft Entra ID. Accounts come only from the directory sync, so there is no sign-up step here:
// whoever Microsoft vouches for must already be linked to an account.
[AllowAnonymous]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet() => RedirectToPage("./Login");

    public async Task<IActionResult> OnPostAsync(string provider, string? returnUrl = null)
    {
        var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
        if (!schemes.Any(s => s.Name == provider))
        {
            return RedirectToPage("./Login");
        }

        // Request a redirect to the external login provider.
        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");
        if (remoteError != null)
        {
            return Fail("Microsoft ile giriş tamamlanamadı.", returnUrl);
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        var directoryId = info == null ? null : EntraSignIn.DirectoryId(info.Principal);
        if (directoryId == null)
        {
            return Fail("Microsoft ile giriş tamamlanamadı.", returnUrl);
        }

        // The temporary sign-in with Microsoft has done its job
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.DirectoryId == directoryId && u.AnonymizedAt == null);
        if (user == null)
        {
            _logger.LogWarning("Microsoft sign-in for an unlinked directory id {DirectoryId}.", directoryId);
            return Fail("Bu Microsoft hesabına bağlı bir kullanıcı yok. BT ekibinden dizin eşitlemesini çalıştırmasını isteyin.", returnUrl);
        }

        // A closed account stays closed, whatever Microsoft says about the person
        if (await _userManager.IsLockedOutAsync(user))
        {
            return RedirectToPage("./Lockout");
        }

        await _signInManager.SignInAsync(user, isPersistent: false, EntraSignIn.Scheme);
        _logger.LogInformation("User logged in with Microsoft.");
        return LocalRedirect(returnUrl);
    }

    private IActionResult Fail(string message, string returnUrl)
    {
        ErrorMessage = message;
        return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
    }
}
