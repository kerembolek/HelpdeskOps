using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Tests;

public class TicketCategoriesTests
{
    [Fact]
    public void EveryCategory_EndsWithOther()
    {
        foreach (var category in Enum.GetValues<TicketCategory>())
            Assert.Equal(TicketSubcategory.Other, TicketCategories.SubcategoriesOf(category)[^1]);
    }

    [Fact]
    public void Belongs_AcceptsSubcategoryOfItsOwnCategory()
    {
        Assert.True(TicketCategories.Belongs(TicketCategory.Network, TicketSubcategory.Vpn));
    }

    [Fact]
    public void Belongs_RejectsSubcategoryOfAnotherCategory()
    {
        // A tampered form could pair a printer problem with the network category
        Assert.False(TicketCategories.Belongs(TicketCategory.Network, TicketSubcategory.PaperJam));
    }

    [Fact]
    public void SubcategoriesOf_ReturnsEmptyForUnknownCategory()
    {
        Assert.Empty(TicketCategories.SubcategoriesOf((TicketCategory)999));
    }

    [Theory]
    [InlineData(DeviceType.Printer, TicketCategory.Printer)]
    [InlineData(DeviceType.Switch, TicketCategory.Network)]
    [InlineData(DeviceType.Router, TicketCategory.Network)]
    [InlineData(DeviceType.Laptop, TicketCategory.Hardware)]
    [InlineData(DeviceType.Monitor, TicketCategory.Hardware)]
    public void For_PicksTheLikelyCategoryOfAScannedDevice(DeviceType deviceType, TicketCategory expected)
    {
        Assert.Equal(expected, TicketCategories.For(deviceType));
    }
}
