using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;

namespace HelpdeskOps.Tests;

public class DeviceCsvImporterTests
{
    private const string Header = "Hostname;IpAddress;SerialNumber;Brand;Model;DeviceType;Status;PurchaseDate;WarrantyEndDate;AssignedEmail";

    [Fact]
    public void Parse_ReadsAValidRow()
    {
        var result = DeviceCsvImporter.Parse(Header + "\nPC-01;10.0.0.5;SN-100;Lenovo;E14;Laptop;;01.03.2025;01.03.2027;test@helpdeskops.com");

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.Line);
        Assert.Equal("PC-01", row.Device.Hostname);
        Assert.Equal(DeviceType.Laptop, row.Device.DeviceType);
        Assert.Equal(new DateTime(2025, 3, 1), row.Device.PurchaseDate);
        Assert.Equal("test@helpdeskops.com", row.AssignedEmail);
    }

    [Fact]
    public void Parse_DefaultsStatusByAssignment()
    {
        var result = DeviceCsvImporter.Parse(Header
            + "\nPC-01;10.0.0.5;SN-1;;;Laptop;;;;user@helpdeskops.com"
            + "\nPC-02;10.0.0.6;SN-2;;;Laptop;;;;");

        Assert.Equal(DeviceStatus.Active, result.Rows[0].Device.Status);
        Assert.Equal(DeviceStatus.InStorage, result.Rows[1].Device.Status);
    }

    [Fact]
    public void Parse_ReportsMissingRequiredColumns()
    {
        var result = DeviceCsvImporter.Parse("Hostname;Brand\nPC-01;Lenovo");

        var error = Assert.Single(result.Errors);
        Assert.Contains("IpAddress", error);
        Assert.Contains("SerialNumber", error);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Parse_ReportsRowErrorsWithLineNumbers()
    {
        var result = DeviceCsvImporter.Parse(Header
            + "\nPC-01;10.0.0.5;SN-1;;;Laptop;;;;"
            + "\nPC-02;999.1.1.1;SN-2;;;Toaster;;;;");

        var error = Assert.Single(result.Errors);
        Assert.StartsWith("Satır 3:", error);
        Assert.Contains("Toaster", error);
    }

    [Fact]
    public void Parse_RejectsDuplicateSerialsInTheFile()
    {
        var result = DeviceCsvImporter.Parse(Header
            + "\nPC-01;10.0.0.5;SN-1;;;Laptop;;;;"
            + "\nPC-02;10.0.0.6;sn-1;;;Laptop;;;;");

        Assert.Contains(result.Errors, e => e.StartsWith("Satır 3:") && e.Contains("birden fazla"));
    }

    [Fact]
    public void Parse_RejectsWarrantyEndingBeforePurchase()
    {
        var result = DeviceCsvImporter.Parse(Header + "\nPC-01;10.0.0.5;SN-1;;;Laptop;;01.06.2025;01.01.2025;");

        Assert.Contains(result.Errors, e => e.Contains("garanti bitişi"));
    }

    [Fact]
    public void Parse_ReportsAnEmptyFile()
    {
        Assert.Equal("Dosya boş.", Assert.Single(DeviceCsvImporter.Parse("").Errors));
    }

    [Fact]
    public void ToCsv_CanBeImportedAgain()
    {
        var first = DeviceCsvImporter.Parse(Header + "\nPC-01;10.0.0.5;SN-100;Lenovo;E14;Laptop;Active;01.03.2025;01.03.2027;a@b.com");
        var csv = System.Text.Encoding.UTF8.GetString(
            DeviceCsvImporter.ToCsv(first.Rows.Select(r => (r.Device, r.AssignedEmail)))).TrimStart('﻿');

        var second = DeviceCsvImporter.Parse(csv);

        Assert.Empty(second.Errors);
        Assert.Equal("SN-100", Assert.Single(second.Rows).Device.SerialNumber);
    }
}
