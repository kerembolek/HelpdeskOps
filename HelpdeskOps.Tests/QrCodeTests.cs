using HelpdeskOps.Services;

namespace HelpdeskOps.Tests;

public class QrCodeTests
{
    [Fact]
    public void Svg_ReturnsScalableSvg()
    {
        var svg = QrCode.Svg("https://helpdesk.example.com/d/5");

        Assert.StartsWith("<svg", svg);
        // A viewBox lets the label CSS decide the printed size
        Assert.Contains("viewBox", svg);
    }

    [Fact]
    public void Svg_DiffersForDifferentDevices()
    {
        Assert.NotEqual(QrCode.Svg("https://helpdesk.example.com/d/5"), QrCode.Svg("https://helpdesk.example.com/d/6"));
    }
}
