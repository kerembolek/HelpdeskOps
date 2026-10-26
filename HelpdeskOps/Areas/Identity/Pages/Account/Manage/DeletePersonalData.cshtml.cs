// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HelpdeskOps.Areas.Identity.Pages.Account.Manage;

// Closed on purpose. Identity's default page deleted the account outright, which failed for anyone with tickets and
// let IT staff delete themselves. Tickets and zimmet forms are company records, so a KVKK deletion request goes to
// IT, who anonymize the account from the user list. This page stays so the default one is not served in its place.
public class DeletePersonalDataModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("./PersonalData");

    public IActionResult OnPost() => RedirectToPage("./PersonalData");
}
