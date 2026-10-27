using System.Security.Cryptography;
using System.Text;
using HelpdeskOps.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

// Receives hardware reports from the PowerShell inventory agent (scripts/Send-Inventory.ps1).
// The agent has no user account, so it proves itself with the shared key from the "Inventory" settings.
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[InventoryKey]
public class InventoryController : ControllerBase
{
    private readonly InventoryImporter _importer;

    public InventoryController(InventoryImporter importer)
    {
        _importer = importer;
    }

    // POST: /api/inventory
    [HttpPost("/api/inventory")]
    public async Task<IActionResult> Report(InventoryReport report)
    {
        var (device, created) = await _importer.ApplyAsync(report);
        return Ok(new { deviceId = device.Id, created });
    }
}

// Checks the agent's key. As an authorization filter it runs before model validation,
// so callers without the key learn nothing about the report format.
public class InventoryKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string Header = "X-Inventory-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var apiKey = context.HttpContext.RequestServices.GetRequiredService<IOptions<InventoryOptions>>().Value.ApiKey;

        // Without a configured key the endpoint does not exist, so a fresh install is not open by default
        if (string.IsNullOrEmpty(apiKey))
        {
            context.Result = new NotFoundResult();
        }
        else if (!IsValidKey(context.HttpContext.Request.Headers[Header].ToString(), apiKey))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    // Compares in constant time, so response timing does not leak how much of a guessed key was right
    public static bool IsValidKey(string? sent, string expected) =>
        !string.IsNullOrEmpty(sent)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(expected));
}
