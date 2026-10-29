using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Tests;

public class ConsumableStockTests
{
    private static Consumable WithStock(int stock, int minimum = 2) =>
        new() { Name = "HP 59A", Kind = ConsumableKind.Toner, Stock = stock, MinimumStock = minimum };

    [Theory]
    [InlineData(3, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(0, 0, true)]
    public void IsLow_AtOrBelowTheMinimum(int stock, int minimum, bool low)
    {
        Assert.Equal(low, WithStock(stock, minimum).IsLow);
    }

    [Fact]
    public void Check_AllowsIncomingAndAvailableStock()
    {
        Assert.Null(ConsumableStock.Check(WithStock(0), 10));
        Assert.Null(ConsumableStock.Check(WithStock(3), -3));
    }

    [Fact]
    public void Check_RefusesMoreThanTheStock()
    {
        Assert.Equal("Stokta yalnızca 1 adet var.", ConsumableStock.Check(WithStock(1), -2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    [InlineData(-1001)]
    public void Check_RefusesZeroAndHugeQuantities(int quantity)
    {
        Assert.Equal("Miktar 1 ile 1000 arasında olmalıdır.", ConsumableStock.Check(WithStock(5000), quantity));
    }
}

public class ConsumableDatabaseTests : KvkkTestDatabase
{
    [Fact]
    public async Task DeletingAPrinter_KeepsItsUsageHistory()
    {
        var printer = new Device
        {
            Hostname = "PR-01", IpAddress = "10.0.0.50", SerialNumber = "PR-SN", DeviceType = DeviceType.Printer, Status = DeviceStatus.Active
        };
        var toner = new Consumable { Name = "HP 59A", Kind = ConsumableKind.Toner, Stock = 1 };
        toner.Movements.Add(new ConsumableMovement { Quantity = -1, Device = printer, CreatedById = "it-1" });
        Context.Consumables.Add(toner);
        await Context.SaveChangesAsync();

        Context.Devices.Remove(printer);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var movement = await Context.ConsumableMovements.SingleAsync();
        Assert.Null(movement.DeviceId);
        Assert.Equal(-1, movement.Quantity);
    }

    [Fact]
    public async Task DeletingAConsumable_RemovesItsHistory()
    {
        var toner = new Consumable { Name = "HP 59A", Stock = 5 };
        toner.Movements.Add(new ConsumableMovement { Quantity = 5 });
        Context.Consumables.Add(toner);
        await Context.SaveChangesAsync();

        Context.Consumables.Remove(toner);
        await Context.SaveChangesAsync();

        Assert.Empty(Context.ConsumableMovements);
    }
}
