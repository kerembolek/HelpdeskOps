using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Tests;

public class EnumExtensionsTests
{
    [Fact]
    public void GetDisplayName_ReturnsTurkishDisplayName()
    {
        Assert.Equal("İşlemde", TicketStatus.InProgress.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_FallsBackToValueForUndefinedMember()
    {
        Assert.Equal("999", ((TicketStatus)999).GetDisplayName());
    }

    [Fact]
    public void EveryTicketStatus_HasADisplayName()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
            Assert.NotEqual(status.ToString(), status.GetDisplayName());
    }
}
