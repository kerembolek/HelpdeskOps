namespace HelpdeskOps.Services;

// Drawn signatures arrive from the signature pad as "data:image/png;base64,..." and are stored as PNG bytes
public static class SignatureImage
{
    private const string DataUrlPrefix = "data:image/png;base64,";
    private const int MaxBytes = 300 * 1024;
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Returns the PNG bytes, or null when the value is missing, too large, or not a PNG
    public static byte[]? FromDataUrl(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith(DataUrlPrefix, StringComparison.Ordinal)
            || dataUrl.Length > DataUrlPrefix.Length + MaxBytes * 4 / 3 + 4)
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dataUrl[DataUrlPrefix.Length..]);
        }
        catch (FormatException)
        {
            return null;
        }

        return bytes.Length <= MaxBytes && bytes.AsSpan().StartsWith(PngHeader) ? bytes : null;
    }

    public static string ToDataUrl(byte[] png) => DataUrlPrefix + Convert.ToBase64String(png);
}
