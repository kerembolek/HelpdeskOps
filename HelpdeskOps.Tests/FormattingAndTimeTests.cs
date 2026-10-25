using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Tests;

public class TurkeyTimeTests
{
    [Fact]
    public void ToTurkeyTime_AddsThreeHoursToUtc()
    {
        var utc = new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 1, 15, 12, 30, 0), utc.ToTurkeyTime());
    }

    [Fact]
    public void ToTurkeyTime_TreatsValuesWithoutKindAsUtc()
    {
        // EF Core returns stored dates without a kind; they are always UTC
        var stored = new DateTime(2026, 7, 1, 21, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(new DateTime(2026, 7, 2, 0, 0, 0), stored.ToTurkeyTime());
    }

    [Fact]
    public void ToUtc_ReversesToTurkeyTime()
    {
        var turkeyNoon = new DateTime(2026, 10, 25, 12, 0, 0);

        Assert.Equal(new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc), TurkeyTime.ToUtc(turkeyNoon));
    }
}

public class DurationFormatTests
{
    [Theory]
    [InlineData(20, "20 dk")]
    [InlineData(60, "1 saat")]
    [InlineData(95, "1 saat 35 dk")]
    [InlineData(1440, "1 gün")]
    [InlineData(1590, "1 gün 2 saat")]
    public void FromMinutes_UsesTheLargestUnits(double minutes, string expected)
    {
        Assert.Equal(expected, DurationFormat.FromMinutes(minutes));
    }
}

public class DeviceTests
{
    private static Device NewDevice(DeviceType type = DeviceType.Laptop, DateTime? warrantyEnd = null) => new()
    {
        Hostname = "PC-01",
        IpAddress = "10.0.0.1",
        SerialNumber = "SN-01",
        DeviceType = type,
        WarrantyEndDate = warrantyEnd
    };

    [Fact]
    public void GetWarrantyState_FollowsTheEndDate()
    {
        var today = TurkeyTime.Today;

        Assert.Equal(WarrantyState.Unknown, NewDevice().GetWarrantyState());
        Assert.Equal(WarrantyState.Expired, NewDevice(warrantyEnd: today.AddDays(-1)).GetWarrantyState());
        Assert.Equal(WarrantyState.ExpiringSoon, NewDevice(warrantyEnd: today.AddDays(Device.WarrantyExpiringSoonDays)).GetWarrantyState());
        Assert.Equal(WarrantyState.Active, NewDevice(warrantyEnd: today.AddDays(Device.WarrantyExpiringSoonDays + 1)).GetWarrantyState());
    }

    [Theory]
    [InlineData(DeviceType.Laptop, true)]
    [InlineData(DeviceType.Desktop, true)]
    [InlineData(DeviceType.Printer, false)]
    [InlineData(DeviceType.Switch, false)]
    public void SupportsRemoteDesktop_OnlyForComputers(DeviceType type, bool expected)
    {
        Assert.Equal(expected, NewDevice(type).SupportsRemoteDesktop());
    }
}
