using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

// Moments are written in Turkey time and converted, so the tests read like an office calendar
public class BusinessCalendarTests
{
    internal static DateTime Tr(int month, int day, int hour, int minute = 0, int year = 2026) =>
        TurkeyTime.ToUtc(new DateTime(year, month, day, hour, minute, 0));

    private static readonly BusinessCalendar Calendar = BusinessCalendar.Default;

    [Fact]
    public void AddWorkingHours_StaysWithinTheDay()
    {
        // Monday 2 November 2026
        Assert.Equal(Tr(11, 2, 18), Calendar.AddWorkingHours(Tr(11, 2, 10), 8));
    }

    [Fact]
    public void AddWorkingHours_CarriesOverToTheNextMorning()
    {
        Assert.Equal(Tr(11, 3, 14), Calendar.AddWorkingHours(Tr(11, 2, 15), 8));
    }

    [Fact]
    public void AddWorkingHours_SkipsTheWeekend()
    {
        // Friday 6 November 16:00: two hours on Friday, six on Monday
        Assert.Equal(Tr(11, 9, 15), Calendar.AddWorkingHours(Tr(11, 6, 16), 8));
    }

    [Fact]
    public void AddWorkingHours_StartsAtOpeningOutsideWorkingHours()
    {
        Assert.Equal(Tr(11, 9, 13), Calendar.AddWorkingHours(Tr(11, 7, 22), 4));
        Assert.Equal(Tr(11, 3, 11), Calendar.AddWorkingHours(Tr(11, 3, 7, 30), 2));
    }

    [Fact]
    public void AddWorkingHours_RespectsRepublicDayAndItsEve()
    {
        // 28 October ends at 13:00 and 29 October is closed
        Assert.Equal(Tr(10, 30, 15), Calendar.AddWorkingHours(Tr(10, 28, 11), 8));
    }

    [Fact]
    public void AddWorkingHours_SkipsKurbanBayrami()
    {
        // Eve on Tuesday 26 May 2026 ends at 13:00, the holiday runs 27-30 May
        Assert.Equal(Tr(6, 1, 12), Calendar.AddWorkingHours(Tr(5, 26, 12), 4));
        Assert.True(Calendar.IsClosed(new DateTime(2026, 3, 20)));
        Assert.Equal(BusinessCalendar.HalfDayEnd, Calendar.HoursOf(new DateTime(2026, 3, 19))!.Value.End);
    }

    [Fact]
    public void Options_SetTheWorkdayAndExtraHolidays()
    {
        var calendar = new BusinessCalendar(new SlaOptions
        {
            WorkdayStart = new TimeSpan(8, 30, 0),
            WorkdayEnd = new TimeSpan(17, 30, 0),
            Holidays = { new DateTime(2026, 11, 3) }
        });

        Assert.True(calendar.IsClosed(new DateTime(2026, 11, 3)));
        // Monday 16:30 + 3 hours: one on Monday, Tuesday closed, two on Wednesday from 08:30
        Assert.Equal(Tr(11, 4, 10, 30), calendar.AddWorkingHours(Tr(11, 2, 16, 30), 3));
    }

    [Fact]
    public void Options_RejectAnEndBeforeTheStart()
    {
        Assert.Throws<ArgumentException>(() => new BusinessCalendar(new SlaOptions { WorkdayStart = new TimeSpan(18, 0, 0), WorkdayEnd = new TimeSpan(9, 0, 0) }));
    }
}

public class TicketSlaTests
{
    private static readonly DateTime Now = BusinessCalendarTests.Tr(11, 4, 12);

    // Saved tickets carry their target, as SlaDueInterceptor sets it
    private static Ticket NewTicket(TicketPriority priority, DateTime createdAt, TicketStatus status = TicketStatus.Open, DateTime? resolvedAt = null) => new()
    {
        Title = "Test",
        Description = "Test",
        RequesterId = "user-1",
        Priority = priority,
        Status = status,
        CreatedAt = createdAt,
        ResolvedAt = resolvedAt,
        SlaDueAt = TicketSla.CalculateDueAt(createdAt, priority, BusinessCalendar.Default)
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
    public void Critical_CountsAroundTheClock()
    {
        var saturdayNight = BusinessCalendarTests.Tr(11, 7, 22);

        Assert.Equal(saturdayNight.AddHours(4), TicketSla.CalculateDueAt(saturdayNight, TicketPriority.Critical, BusinessCalendar.Default));
        Assert.Equal("4 saat (7/24)", TicketSla.TargetText(TicketPriority.Critical));
        Assert.Equal("8 mesai saati", TicketSla.TargetText(TicketPriority.High));
    }

    [Fact]
    public void Others_CountWorkingHours()
    {
        // High priority opened Friday afternoon is due on Monday
        Assert.Equal(BusinessCalendarTests.Tr(11, 9, 15),
            TicketSla.CalculateDueAt(BusinessCalendarTests.Tr(11, 6, 16), TicketPriority.High, BusinessCalendar.Default));
    }

    [Fact]
    public void StateOf_ResolvedWithinTarget_IsMet()
    {
        // Monday 10:00, eight working hours: due at 18:00 on Monday
        var ticket = NewTicket(TicketPriority.High, BusinessCalendarTests.Tr(11, 2, 10), TicketStatus.Resolved, BusinessCalendarTests.Tr(11, 2, 17));

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
        // High, opened Wednesday 09:00, due 17:00; at 16:00 one of eight hours is left
        var ticket = NewTicket(TicketPriority.High, BusinessCalendarTests.Tr(11, 4, 9));

        Assert.Equal(SlaState.DueSoon, TicketSla.StateOf(ticket, BusinessCalendarTests.Tr(11, 4, 16)));
        Assert.Equal(SlaState.OnTrack, TicketSla.StateOf(ticket, BusinessCalendarTests.Tr(11, 4, 12)));
    }

    [Fact]
    public void StateOf_FreshActiveTicket_IsOnTrack()
    {
        var ticket = NewTicket(TicketPriority.Low, Now.AddHours(-1));

        Assert.Equal(SlaState.OnTrack, TicketSla.StateOf(ticket, Now));
    }

    [Fact]
    public void DueAt_FallsBackForUnsavedTickets()
    {
        var ticket = NewTicket(TicketPriority.High, BusinessCalendarTests.Tr(11, 6, 16));
        var expected = ticket.SlaDueAt;
        ticket.SlaDueAt = null;

        Assert.Equal(expected, TicketSla.DueAt(ticket));
    }

    [Fact]
    public void IsOverdue_MatchesOnlyActiveTicketsPastTheirTarget()
    {
        var isOverdue = TicketSla.IsOverdue(Now).Compile();

        Assert.True(isOverdue(NewTicket(TicketPriority.Critical, Now.AddHours(-5))));
        // Low priority from three weeks ago is well past 72 working hours
        Assert.True(isOverdue(NewTicket(TicketPriority.Low, Now.AddDays(-21), TicketStatus.InProgress)));
        Assert.False(isOverdue(NewTicket(TicketPriority.Low, Now.AddHours(-5))));
        Assert.False(isOverdue(NewTicket(TicketPriority.Critical, Now.AddHours(-5), TicketStatus.Closed, Now.AddHours(-1))));
    }

    [Fact]
    public void MetTarget_CountsOnlyTicketsResolvedInTime()
    {
        var metTarget = TicketSla.MetTarget().Compile();
        var monday = BusinessCalendarTests.Tr(11, 2, 10);

        Assert.True(metTarget(NewTicket(TicketPriority.High, monday, TicketStatus.Resolved, monday.AddHours(7))));
        Assert.False(metTarget(NewTicket(TicketPriority.High, monday, TicketStatus.Resolved, monday.AddDays(1))));
        Assert.False(metTarget(NewTicket(TicketPriority.High, monday)));
    }
}

// The interceptor on a real (SQLite) database, as the app uses it
public class SlaDueInterceptorTests : KvkkTestDatabase
{
    private static readonly SlaDueInterceptor Interceptor = new(Options.Create(new SlaOptions()));

    public SlaDueInterceptorTests() : base(Interceptor)
    {
    }

    [Fact]
    public async Task NewTickets_GetATarget()
    {
        Assert.All(await Context.Tickets.ToListAsync(), t => Assert.NotNull(t.SlaDueAt));

        var friday = BusinessCalendarTests.Tr(11, 6, 16);
        Context.Tickets.Add(new Ticket
        {
            Title = "Yeni", Description = "-", RequesterId = "emp-1", Priority = TicketPriority.High,
            Category = TicketCategory.Other, CreatedAt = friday
        });
        await Context.SaveChangesAsync();

        Assert.Equal(BusinessCalendarTests.Tr(11, 9, 15), (await Context.Tickets.SingleAsync(t => t.Title == "Yeni")).SlaDueAt);
    }

    [Fact]
    public async Task PriorityChange_MovesTheTarget()
    {
        var ticket = await Context.Tickets.FirstAsync();
        ticket.Priority = TicketPriority.Critical;
        await Context.SaveChangesAsync();

        Assert.Equal(ticket.CreatedAt.AddHours(4), ticket.SlaDueAt);
    }

    [Fact]
    public async Task Backfill_FillsOldTickets()
    {
        await Context.Tickets.ExecuteUpdateAsync(s => s.SetProperty(t => t.SlaDueAt, (DateTime?)null));

        Assert.Equal(2, await Interceptor.BackfillAsync(Context));
        Context.ChangeTracker.Clear();
        Assert.All(await Context.Tickets.ToListAsync(), t => Assert.NotNull(t.SlaDueAt));
        Assert.Equal(0, await Interceptor.BackfillAsync(Context));
    }
}
