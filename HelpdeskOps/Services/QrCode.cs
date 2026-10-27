using QRCoder;

namespace HelpdeskOps.Services;

// Renders QR codes as inline SVG, so they print sharp at any size and need no image files.
// Error correction level Q still reads when a quarter of the code is scratched or dirty, which suits stickers.
public static class QrCode
{
    public static string Svg(string text, int pixelsPerModule = 5)
    {
        using var data = new QRCodeGenerator().CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(pixelsPerModule, "#000000", "#ffffff", drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
