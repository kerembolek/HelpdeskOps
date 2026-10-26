using System.Net;
using System.Security.Claims;
using System.Text.Json;
using HelpdeskOps.Models;
using HelpdeskOps.Services.Kvkk;
using Microsoft.AspNetCore.Http;

namespace HelpdeskOps.Tests;

public class PersonalDataExporterTests : KvkkTestDatabase
{
    private async Task<JsonElement> ExportAsync(string userId)
    {
        var bytes = await new PersonalDataExporter(Context).ExportAsync(userId);
        return JsonDocument.Parse(bytes!).RootElement;
    }

    [Fact]
    public async Task Export_HasTheAccountAndThePersonsOwnRecords()
    {
        var data = await ExportAsync("emp-1");

        var account = data.GetProperty("Hesap");
        Assert.Equal("Onur", account.GetProperty("Ad").GetString());
        Assert.Equal("onur@x.com", account.GetProperty("E-posta").GetString());
        Assert.Equal("Çalışan", account.GetProperty("Roller")[0].GetString());

        var ticket = Assert.Single(data.GetProperty("Açtığınız Talepler").EnumerateArray());
        Assert.Equal("Yazıcı çalışmıyor", ticket.GetProperty("Başlık").GetString());
        Assert.Equal("Hâlâ yazmıyor", Assert.Single(data.GetProperty("Yorumlarınız").EnumerateArray()).GetProperty("Yorum").GetString());
        Assert.Equal("Onur Güçlü", Assert.Single(data.GetProperty("Zimmet Formları").EnumerateArray()).GetProperty("Ad Soyad").GetString());
        Assert.Single(data.GetProperty("Bildirimler").EnumerateArray());
        Assert.Equal("10.0.0.9", Assert.Single(data.GetProperty("Yaptığınız İşlemler (Denetim Kaydı)").EnumerateArray()).GetProperty("IP Adresi").GetString());
    }

    [Fact]
    public async Task Export_LeavesOutOtherPeoplesRecords()
    {
        var data = await ExportAsync("it-1");

        Assert.Equal("BT'nin kendi talebi", Assert.Single(data.GetProperty("Açtığınız Talepler").EnumerateArray()).GetProperty("Başlık").GetString());
        Assert.Equal("Yazıcı çalışmıyor", Assert.Single(data.GetProperty("Size Atanan Talepler").EnumerateArray()).GetProperty("Başlık").GetString());
        Assert.Empty(data.GetProperty("Yorumlarınız").EnumerateArray());
        Assert.Empty(data.GetProperty("Zimmet Formları").EnumerateArray());
        Assert.Equal("Yeni talep", Assert.Single(data.GetProperty("Bildirimler").EnumerateArray()).GetProperty("Mesaj").GetString());
    }

    [Fact]
    public async Task Export_OfAnUnknownUser_IsNull()
    {
        Assert.Null(await new PersonalDataExporter(Context).ExportAsync("nobody"));
    }

    [Fact]
    public async Task RecordExport_WritesWhoDownloadedWhoseData()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "it-1"),
                new Claim(ClaimTypes.Name, "it@x.com")
            }, "test"))
        };
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");

        await new PersonalDataExporter(Context).RecordExportAsync("emp-1", http);

        var log = Context.AuditLogs.Single(a => a.Action == AuditAction.Exported);
        Assert.Equal("it-1", log.UserId);
        Assert.Equal("10.0.0.7", log.IpAddress);
        Assert.Equal("emp-1", log.EntityId);
        Assert.Equal("onur@x.com", log.EntityLabel);
    }

    [Fact]
    public void FileName_UsesAsciiLettersOnly()
    {
        var user = new ApplicationUser { FirstName = "İlkay Şükrü", LastName = "Güçlüoğlu" };
        Assert.Matches(@"^kisisel-veriler-ilkay-sukru-gucluoglu-\d{4}-\d{2}-\d{2}\.json$", PersonalDataExporter.FileName(user));
    }
}
