// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using HelpdeskOps.Models;
using HelpdeskOps.Services.Kvkk;

namespace HelpdeskOps.Areas.Identity.Pages.Account.Manage;

// "Kişisel Verilerim": download your data, and how correction and deletion work
public class PersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly KvkkOptions _kvkk;

    public PersonalDataModel(UserManager<ApplicationUser> userManager, IOptions<KvkkOptions> kvkk)
    {
        _userManager = userManager;
        _kvkk = kvkk.Value;
    }

    public string? ContactEmail => _kvkk.ContactEmail;

    public async Task<IActionResult> OnGet()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        return Page();
    }
}
