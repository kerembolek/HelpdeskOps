using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

// The two-level category tree. Every category ends with "Other", which asks for a short free-text description.
public static class TicketCategories
{
    private static readonly Dictionary<TicketCategory, TicketSubcategory[]> Tree = new()
    {
        [TicketCategory.Hardware] =
        [
            TicketSubcategory.PcWontStart, TicketSubcategory.SlowOrFreezing, TicketSubcategory.Display,
            TicketSubcategory.KeyboardMouse, TicketSubcategory.PartFailure, TicketSubcategory.Other
        ],
        [TicketCategory.Software] =
        [
            TicketSubcategory.Installation, TicketSubcategory.AppError, TicketSubcategory.License,
            TicketSubcategory.WindowsUpdate, TicketSubcategory.Other
        ],
        [TicketCategory.Network] =
        [
            TicketSubcategory.NoInternet, TicketSubcategory.WiFi, TicketSubcategory.Vpn,
            TicketSubcategory.SharedFolder, TicketSubcategory.Other
        ],
        [TicketCategory.EmailOutlook] =
        [
            TicketSubcategory.OutlookWontOpen, TicketSubcategory.SendReceive, TicketSubcategory.MailboxFull,
            TicketSubcategory.NewMailbox, TicketSubcategory.Other
        ],
        [TicketCategory.Printer] =
        [
            TicketSubcategory.NotPrinting, TicketSubcategory.PaperJam, TicketSubcategory.Toner,
            TicketSubcategory.Scanning, TicketSubcategory.Other
        ],
        [TicketCategory.Other] = [TicketSubcategory.Other]
    };

    public static IReadOnlyList<TicketSubcategory> SubcategoriesOf(TicketCategory category) =>
        Tree.TryGetValue(category, out var subcategories) ? subcategories : [];

    public static bool Belongs(TicketCategory category, TicketSubcategory subcategory) =>
        SubcategoriesOf(category).Contains(subcategory);

    // The category a ticket for this kind of device most likely belongs to, used when a device sticker is scanned
    public static TicketCategory For(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Printer => TicketCategory.Printer,
        DeviceType.Switch or DeviceType.Router => TicketCategory.Network,
        _ => TicketCategory.Hardware
    };
}
