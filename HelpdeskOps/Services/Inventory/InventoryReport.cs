using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Services.Inventory;

// What the PowerShell agent sends from each computer
public class InventoryReport : IValidatableObject
{
    [Required, StringLength(63)]
    public string Hostname { get; set; } = "";

    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = "";

    [Required]
    [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$")]
    public string IpAddress { get; set; } = "";

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    // Only computers run the agent; sent as "Laptop" or "Desktop"
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [AllowedValues(DeviceType.Laptop, DeviceType.Desktop)]
    public DeviceType DeviceType { get; set; } = DeviceType.Desktop;

    [StringLength(100)]
    public string? Processor { get; set; }

    [Range(0, 4096)]
    public int? RamGb { get; set; }

    [Range(0, 1_000_000)]
    public int? DiskGb { get; set; }

    [StringLength(100)]
    public string? OsVersion { get; set; }

    // Values firmware vendors leave in place of a real serial number. Custom-built PCs often report one of these,
    // and accepting them would merge every such computer into a single device.
    private static readonly string[] PlaceholderSerials =
    {
        "system serial number", "to be filled by o.e.m.", "default string", "not specified", "not applicable",
        "none", "n/a", "na", "0", "00000000", "0123456789", "123456789", "serial number", "chassis serial number"
    };

    public static bool IsPlaceholderSerial(string? serial) =>
        string.IsNullOrWhiteSpace(serial) || PlaceholderSerials.Contains(serial.Trim().ToLowerInvariant());

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsPlaceholderSerial(SerialNumber))
        {
            yield return new ValidationResult("The serial number is a firmware placeholder, not a real serial number.",
                new[] { nameof(SerialNumber) });
        }
    }
}
