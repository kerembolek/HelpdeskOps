namespace HelpdeskOps.Models;

// The "Sla" settings section: the working day and any extra days off a company gives (bridge days, a company holiday)
public class SlaOptions
{
    public const string SectionName = "Sla";

    public TimeSpan WorkdayStart { get; set; } = new(9, 0, 0);

    public TimeSpan WorkdayEnd { get; set; } = new(18, 0, 0);

    // Extra closed days, as "2026-12-31"
    public List<DateTime> Holidays { get; set; } = new();
}

// Working hours in Turkey time: weekdays only, closed on Turkish public holidays, and closed from 13:00 on the
// eves (arife) of the religious holidays and on 28 October, as in the official calendar
public class BusinessCalendar
{
    public static readonly TimeSpan HalfDayEnd = new(13, 0, 0);

    // Ramazan and Kurban Bayramı move every year: the first day of each, from the Diyanet calendar.
    // Ramazan lasts 3 days and Kurban 4; the day before each is a half day. Later years go in "Sla:Holidays".
    private static readonly (DateTime Ramazan, DateTime Kurban)[] ReligiousHolidays =
    {
        (new(2025, 3, 30), new(2025, 6, 6)),
        (new(2026, 3, 20), new(2026, 5, 27)),
        (new(2027, 3, 9), new(2027, 5, 16)),
        (new(2028, 2, 26), new(2028, 5, 5)),
        (new(2029, 2, 14), new(2029, 4, 24)),
        (new(2030, 2, 4), new(2030, 4, 13)),
    };

    // Same date every year
    private static readonly (int Month, int Day)[] NationalHolidays =
    {
        (1, 1), (4, 23), (5, 1), (5, 19), (7, 15), (8, 30), (10, 29)
    };

    private readonly SlaOptions _options;
    private readonly HashSet<DateTime> _closedDays;
    private readonly HashSet<DateTime> _halfDays;

    public BusinessCalendar(SlaOptions options)
    {
        if (options.WorkdayEnd <= options.WorkdayStart)
            throw new ArgumentException("Sla:WorkdayEnd must be after Sla:WorkdayStart.");

        _options = options;
        _closedDays = options.Holidays.Select(d => d.Date).ToHashSet();
        _halfDays = new HashSet<DateTime>();
        foreach (var (ramazan, kurban) in ReligiousHolidays)
        {
            AddHoliday(ramazan, 3);
            AddHoliday(kurban, 4);
        }
    }

    public static BusinessCalendar Default { get; } = new(new SlaOptions());

    private void AddHoliday(DateTime first, int days)
    {
        _halfDays.Add(first.AddDays(-1));
        for (var i = 0; i < days; i++)
            _closedDays.Add(first.AddDays(i));
    }

    public bool IsClosed(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
        || _closedDays.Contains(date.Date)
        || NationalHolidays.Contains((date.Month, date.Day));

    // The working hours of a calendar day in Turkey time, or null when the office is closed
    public (TimeSpan Start, TimeSpan End)? HoursOf(DateTime date)
    {
        if (IsClosed(date))
            return null;

        var end = _options.WorkdayEnd;
        var halfDay = _halfDays.Contains(date.Date) || (date.Month == 10 && date.Day == 28);
        if (halfDay && end > HalfDayEnd)
            end = HalfDayEnd > _options.WorkdayStart ? HalfDayEnd : _options.WorkdayStart;
        return end > _options.WorkdayStart ? (_options.WorkdayStart, end) : null;
    }

    // The UTC moment after the given number of working hours have passed from a UTC start.
    // A ticket opened outside working hours starts counting when the office next opens.
    public DateTime AddWorkingHours(DateTime utcStart, double hours)
    {
        var local = utcStart.ToTurkeyTime();
        var remaining = TimeSpan.FromHours(hours);

        // Ten years of closed days is far more than any target needs; the limit only guards against bad settings
        for (var day = 0; day < 3660; day++)
        {
            var date = local.Date.AddDays(day);
            if (HoursOf(date) is not { } workday)
                continue;
            var (start, end) = workday;

            var from = day == 0 && local.TimeOfDay > start ? local.TimeOfDay : start;
            if (from >= end)
                continue;

            var available = end - from;
            if (remaining <= available)
                return TurkeyTime.ToUtc(date + from + remaining);
            remaining -= available;
        }
        throw new InvalidOperationException("No working hours found within ten years.");
    }
}
