using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Predictions;

public static class WorkCalendarRules
{
    public static void ValidateMonth(int year, int month)
    {
        if (year is < 1900 or > 9998 || month is < 1 or > 12)
        {
            throw new AppException("InvalidRequest");
        }
    }

    public static string MonthId(DateOnly date) => $"work-calendar/{date.Year:D4}-{date.Month:D2}";

    public static int DefaultPercent(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || Holiday(date) is not null ? 0 : 100;

    public static string? Holiday(DateOnly date)
    {
        var fixedHoliday = (date.Month, date.Day) switch
        {
            (1, 1) => "NewYear", (5, 1) => "LabourDay", (5, 8) => "LiberationDay",
            (7, 5) => "CyrilMethodius", (7, 6) => "JanHus", (9, 28) => "StatehoodDay",
            (10, 28) => "IndependenceDay", (11, 17) => "FreedomDay",
            (12, 24) => "ChristmasEve", (12, 25) => "ChristmasDay", (12, 26) => "StStephen",
            _ => null
        };
        if (fixedHoliday is not null)
        {
            return fixedHoliday;
        }
        var easter = Easter(date.Year);
        return date == easter.AddDays(-2) ? "GoodFriday" : date == easter.AddDays(1) ? "EasterMonday" : null;
    }

    public static DateTimeOffset DayStart(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }
        return zone.IsAmbiguousTime(local)
            ? new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).ToUniversalTime()
            : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static DateOnly Easter(int year)
    {
        // Gregorian computus (Meeus/Jones/Butcher), including movable Czech holidays.
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var value = h + l - 7 * m + 114;
        return new DateOnly(year, value / 31, value % 31 + 1);
    }
}
