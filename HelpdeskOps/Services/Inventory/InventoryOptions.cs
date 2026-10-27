namespace HelpdeskOps.Services.Inventory;

public class InventoryOptions
{
    public const string SectionName = "Inventory";

    // Shared secret the agent sends in the X-Inventory-Key header; empty turns the endpoint off
    public string? ApiKey { get; set; }
}
