using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Services;

public record DeviceImportRow(int Line, Device Device, string? AssignedEmail);

public class DeviceImportResult
{
    public List<DeviceImportRow> Rows { get; } = new();
    public List<string> Errors { get; } = new();
}

// Reads devices from a CSV file (semicolon or comma separated, header row first).
// Checks that only need the file are done here; checks against the database are done by DevicesController.
public static class DeviceCsvImporter
{
    public const int MaxRows = 500;
    public const long MaxFileSize = 1024 * 1024;

    public static readonly string[] Columns =
    {
        "Hostname", "IpAddress", "SerialNumber", "Brand", "Model", "DeviceType",
        "Status", "PurchaseDate", "WarrantyEndDate", "AssignedEmail"
    };

    private static readonly string[] RequiredColumns = { "Hostname", "IpAddress", "SerialNumber", "DeviceType" };

    private static readonly string[] DateFormats = { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd" };

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static DeviceImportResult Parse(string text)
    {
        var result = new DeviceImportResult();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
        {
            result.Errors.Add("Dosya boş.");
            return result;
        }

        var separator = lines[0].Contains(';') ? ';' : ',';
        var header = SplitLine(lines[0], separator).Select(h => h.Trim()).ToList();
        var index = Columns.ToDictionary(c => c, c => header.FindIndex(h => h.Equals(c, StringComparison.OrdinalIgnoreCase)));

        var missing = RequiredColumns.Where(c => index[c] < 0).ToList();
        if (missing.Count > 0)
        {
            result.Errors.Add($"Başlık satırında eksik sütun: {string.Join(", ", missing)}. Şablonu indirip kullanabilirsiniz.");
            return result;
        }

        var dataLines = lines.Skip(1).Select((line, i) => (Line: i + 2, Text: line)).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
        if (dataLines.Count == 0)
        {
            result.Errors.Add("Dosyada cihaz satırı yok.");
            return result;
        }
        if (dataLines.Count > MaxRows)
        {
            result.Errors.Add($"Tek seferde en fazla {MaxRows} cihaz içe aktarılabilir.");
            return result;
        }

        var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (line, rowText) in dataLines)
        {
            var cells = SplitLine(rowText, separator);
            string? Cell(string column) =>
                index[column] >= 0 && index[column] < cells.Count && !string.IsNullOrWhiteSpace(cells[index[column]])
                    ? cells[index[column]].Trim()
                    : null;

            var errors = new List<string>();

            var type = ParseEnum<DeviceType>(Cell("DeviceType"));
            if (type == null)
                errors.Add($"geçersiz cihaz türü \"{Cell("DeviceType")}\"");

            DeviceStatus? status = null;
            if (Cell("Status") is string statusText && (status = ParseEnum<DeviceStatus>(statusText)) == null)
                errors.Add($"geçersiz durum \"{statusText}\"");

            var purchaseDate = ParseDate(Cell("PurchaseDate"), "satın alma tarihi", errors);
            var warrantyEnd = ParseDate(Cell("WarrantyEndDate"), "garanti bitişi", errors);

            var device = new Device
            {
                Hostname = Cell("Hostname") ?? "",
                IpAddress = Cell("IpAddress") ?? "",
                SerialNumber = Cell("SerialNumber") ?? "",
                Brand = Cell("Brand"),
                Model = Cell("Model"),
                DeviceType = type ?? DeviceType.Other,
                PurchaseDate = purchaseDate,
                WarrantyEndDate = warrantyEnd,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };
            var assignedEmail = Cell("AssignedEmail");
            device.Status = status ?? (assignedEmail != null ? DeviceStatus.Active : DeviceStatus.InStorage);

            // The same attribute rules as the device form (required fields, IPv4 format, lengths)
            var validation = new List<ValidationResult>();
            Validator.TryValidateObject(device, new ValidationContext(device), validation, validateAllProperties: true);
            errors.AddRange(validation.Select(v => v.ErrorMessage ?? "geçersiz değer"));

            if (purchaseDate?.Date > DateTime.Today)
                errors.Add("satın alma tarihi ileri bir tarih olamaz");
            if (purchaseDate != null && warrantyEnd?.Date < purchaseDate.Value.Date)
                errors.Add("garanti bitişi satın alma tarihinden önce olamaz");
            if (device.SerialNumber.Length > 0 && !seenSerials.Add(device.SerialNumber))
                errors.Add($"seri numarası {device.SerialNumber} dosyada birden fazla kez geçiyor");

            if (errors.Count > 0)
                result.Errors.Add($"Satır {line}: {string.Join("; ", errors)}");
            else
                result.Rows.Add(new DeviceImportRow(line, device, assignedEmail));
        }

        return result;
    }

    // Accepts the member name ("Laptop") or the Turkish label ("Dizüstü Bilgisayar"), ignoring case
    private static TEnum? ParseEnum<TEnum>(string? text) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var byName) && Enum.IsDefined(byName))
            return byName;
        return Enum.GetValues<TEnum>()
            .Where(v => string.Compare(v.GetDisplayName(), text.Trim(), Turkish, CompareOptions.IgnoreCase) == 0)
            .Select(v => (TEnum?)v)
            .FirstOrDefault();
    }

    private static DateTime? ParseDate(string? text, string label, List<string> errors)
    {
        if (text == null)
            return null;
        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        errors.Add($"{label} \"{text}\" anlaşılamadı (gg.aa.yyyy veya yyyy-aa-gg)");
        return null;
    }

    // Splits one CSV line, honoring double-quoted fields with "" escapes
    private static List<string> SplitLine(string line, char separator)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == separator) { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(c);
        }
        cells.Add(cell.ToString());
        return cells;
    }
}
