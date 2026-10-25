using HelpdeskOps.Data;
using HelpdeskOps.Services;

namespace HelpdeskOps.Tests;

public class SignatureImageTests
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public void FromDataUrl_AcceptsAPng()
    {
        var png = DemoSignature.Create("Ayşe Demir");

        var parsed = SignatureImage.FromDataUrl(SignatureImage.ToDataUrl(png));

        Assert.Equal(png, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("data:image/jpeg;base64,AAAA")]
    [InlineData("data:image/png;base64,not-base64!")]
    public void FromDataUrl_RejectsMissingOrMalformedValues(string? dataUrl)
    {
        Assert.Null(SignatureImage.FromDataUrl(dataUrl));
    }

    [Fact]
    public void FromDataUrl_RejectsBytesThatAreNotAPng()
    {
        var gif = "GIF89a"u8.ToArray();

        Assert.Null(SignatureImage.FromDataUrl("data:image/png;base64," + Convert.ToBase64String(gif)));
    }

    [Fact]
    public void FromDataUrl_RejectsImagesOverTheSizeLimit()
    {
        var huge = PngHeader.Concat(new byte[400 * 1024]).ToArray();

        Assert.Null(SignatureImage.FromDataUrl(SignatureImage.ToDataUrl(huge)));
    }

    [Fact]
    public void DemoSignature_DiffersPerName()
    {
        Assert.NotEqual(DemoSignature.Create("Onur Güçlü"), DemoSignature.Create("Ayşe Demir"));
    }
}
