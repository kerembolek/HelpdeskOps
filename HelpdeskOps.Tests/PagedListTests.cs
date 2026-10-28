using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Tests;

public class PagedListTests
{
    private static readonly int[] Numbers = Enumerable.Range(1, 55).ToArray();

    [Fact]
    public void Create_ReturnsTheRequestedPage()
    {
        var page = PagedList<int>.Create(Numbers, 2, 25);

        Assert.Equal(Enumerable.Range(26, 25), page);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.PageCount);
        Assert.Equal(55, page.Total);
    }

    [Fact]
    public void Create_LastPageHoldsTheRemainder()
    {
        Assert.Equal([51, 52, 53, 54, 55], PagedList<int>.Create(Numbers, 3, 25));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-4, 1)]
    [InlineData(99, 3)]
    public void Create_ClampsPageNumbersOutsideTheList(int requested, int expected)
    {
        // An old bookmark or a typed URL must not show an empty page
        Assert.Equal(expected, PagedList<int>.Create(Numbers, requested, 25).Page);
    }

    [Fact]
    public void Create_EmptyListHasOnePage()
    {
        var page = PagedList<int>.Create([], 1, 25);

        Assert.Empty(page);
        Assert.Equal(1, page.PageCount);
    }

    [Fact]
    public async Task CreateAsync_LoadsOnlyTheRequestedPageFromTheDatabase()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        context.Database.EnsureCreated();
        for (var i = 1; i <= 30; i++)
        {
            context.Devices.Add(new Device
            {
                Hostname = $"PC-{i:00}", IpAddress = "10.0.0.1", SerialNumber = $"SN-{i}",
                DeviceType = DeviceType.Desktop, Status = DeviceStatus.Active
            });
        }
        await context.SaveChangesAsync();

        var page = await PagedList<Device>.CreateAsync(context.Devices.OrderBy(d => d.Hostname), 2, 25);

        Assert.Equal(30, page.Total);
        Assert.Equal(2, page.PageCount);
        Assert.Equal(["PC-26", "PC-27", "PC-28", "PC-29", "PC-30"], page.Select(d => d.Hostname));
    }
}
