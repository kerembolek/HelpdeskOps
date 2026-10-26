// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HelpdeskOps.Models;
using HelpdeskOps.Services.Kvkk;

namespace HelpdeskOps.Areas.Identity.Pages.Account.Manage;

// Sends the signed-in user everything HelpdeskOps keeps about them, instead of Identity's default file with only the account fields
public class DownloadPersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PersonalDataExporter _exporter;

    public DownloadPersonalDataModel(UserManager<ApplicationUser> userManager, PersonalDataExporter exporter)
    {
        _userManager = userManager;
        _exporter = exporter;
    }

    public IActionResult OnGet()
    {
        return NotFound();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        var data = await _exporter.ExportAsync(user.Id);
        await _exporter.RecordExportAsync(user.Id, HttpContext);
        return File(data!, "application/json", PersonalDataExporter.FileName(user));
    }
}
