using System.Globalization;

namespace WhatHaveIDone.Web.Services;

public static class UserTime
{
    public static DateTime Local(DateTimeOffset instant, string zone) => TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime;
    public static DateTimeOffset Instant(DateTime local, string zone, string? offset = null)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local)) throw new FormatException(Resources.Strings.InvalidLocalTime);
        if (timeZone.IsAmbiguousTime(local))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(local);
            if (!TimeSpan.TryParse(offset, CultureInfo.InvariantCulture, out var chosen) || !offsets.Contains(chosen)) throw new FormatException(Resources.Strings.InvalidLocalTime);
            return new DateTimeOffset(local, chosen).ToUniversalTime();
        }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone));
    }
    public static string Input(DateTimeOffset instant, string zone) => Local(instant, zone).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
    public static DateTimeOffset Parse(string value, string zone, string? offset = null) => Instant(DateTime.Parse(value, CultureInfo.InvariantCulture), zone, offset);
    public static string Offset(DateTimeOffset instant, string zone) => TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zone)).Offset.ToString();
    public static string Duration(double seconds)
    { var span = TimeSpan.FromSeconds(Math.Max(0, seconds)); return $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"; }
    public static double Seconds(DateTimeOffset start, DateTimeOffset? end, DateTimeOffset now, DateTimeOffset from, DateTimeOffset to)
        => Math.Max(0, ((end ?? now) < to ? end ?? now : to).Subtract(start > from ? start : from).TotalSeconds);
}
