using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Tests;

public class TicketSlaTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket(TicketPriority priority, DateTime createdAt, TicketStatus status = TicketStatus.Open, DateTime? resolvedAt = null) => new()
    {
        Title = "Test",
        Description = "Test",
        RequesterId = "user-1",
        Priority = priority,
        Status = status,
        CreatedAt = createdAt,
        ResolvedAt = resolvedAt
    };

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void TargetHours_FollowPriority(TicketPriority priority, int hours)
    {
        Assert.Equal(hours, TicketSla.TargetHours(priority));
    }

    [Fact]
    public void StateOf_ResolvedWithinTarget_IsMet()
    {
        var ticket = NewTicket(TicketPriority.High, Now.AddHours(-10), TicketStatus.Resolved, Now.AddHours(-3));

        Assert.Equal(SlaState.Met, TicketSla.StateOf(ticket, Now));
    }

    [Fact]
    public void StateOf_ResolvedAfterTarget_IsMissed()
    {
        var ticket = NewTicket(TicketPriority.Critical, Now.AddHours(-10), TicketStatus.Resolved, Now.AddHours(-1));

        Assert.Equal(SlaState.Missed, TicketSla.StateOf(ticket, Now));
    }

    [Fact]
    public void StateOf_ActivePastTarget_IsOverdue()
    {
        var ticket = NewTicket(TicketPriority.Critical, Now.AddHours(-5));

        Assert.Equal(SlaState.Overdue, TicketSla.StateOf(ticket, Now));
    }

    [Fact]
    public void StateOf_ActiveWithLessThanAQuarterLeft_IsDueSoon()
    {
        // 24 hour target, 20 hours gone: 4 hours left is under the 6 hour quarter
        var ticket = NewTicket(TicketPriority.Medium, Now.AddHours(-20));

        Assert.Equal(SlaState.DueSoon, TicketSla.StateOf(ticket, Now));
    }

    [Fact]
    public void StateOf_FreshActiveTicket_IsOnTrack()
    {
        var ticket = NewTicket(TicketPriority.Low, Now.AddHours(-1));

        Assert.Equal(SlaState.OnTrack, TicketSla.StateOf(ticket, Now));
    }

    [Fact]
    public void IsOverdue_MatchesOnlyActiveTicketsPastTheirTarget()
    {
        var isOverdue = TicketSla.IsOverdue(Now).Compile();

        Assert.True(isOverdue(NewTicket(TicketPriority.Critical, Now.AddHours(-5))));
        Assert.True(isOverdue(NewTicket(TicketPriority.Low, Now.AddHours(-80), TicketStatus.InProgress)));
        Assert.False(isOverdue(NewTicket(TicketPriority.Low, Now.AddHours(-5))));
        Assert.False(isOverdue(NewTicket(TicketPriority.Critical, Now.AddHours(-5), TicketStatus.Closed, Now.AddHours(-1))));
    }

    [Fact]
    public void MetTarget_CountsOnlyTicketsResolvedInTime()
    {
        var metTarget = TicketSla.MetTarget().Compile();

        Assert.True(metTarget(NewTicket(TicketPriority.High, Now.AddHours(-10), TicketStatus.Resolved, Now.AddHours(-3))));
        Assert.False(metTarget(NewTicket(TicketPriority.High, Now.AddHours(-10), TicketStatus.Resolved, Now)));
        Assert.False(metTarget(NewTicket(TicketPriority.High, Now.AddHours(-1))));
    }
}
