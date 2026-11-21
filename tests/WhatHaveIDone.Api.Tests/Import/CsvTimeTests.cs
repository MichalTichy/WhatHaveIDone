using WhatHaveIDone.API.Import;

namespace WhatHaveIDone.Api.Tests;

public sealed class CsvTimeTests
{
    private static TimeZoneInfo Prague=>TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    [Fact] public void DurationDisambiguatesTheAutumnClockChange()
    {
        var interval=TogglCsvReader.ParseInterval("2026-10-25","01:30:00","2026-10-25","02:30:00","02:00:00",Prague);
        Assert.Equal(7200,(interval.End-interval.Start).TotalSeconds);Assert.Equal(DateTimeOffset.Parse("2026-10-25T01:30Z"),interval.End);
    }
    [Fact] public void UnresolvableAmbiguousOrNonexistentTimesAreNotGuessed()
    {
        Assert.Throws<FormatException>(()=>TogglCsvReader.ParseInterval("2026-10-25","02:15:00","2026-10-25","02:45:00","00:30:00",Prague));
        Assert.Throws<FormatException>(()=>TogglCsvReader.ParseInterval("2026-03-29","02:15:00","2026-03-29","03:15:00","01:00:00",Prague));
    }
    [Fact] public void AWholeSpringDayIsTwentyThreeHours()
    {
        var interval=TogglCsvReader.ParseInterval("2026-03-29","00:00:00","2026-03-30","00:00:00","23:00:00",Prague);Assert.Equal(82800,(interval.End-interval.Start).TotalSeconds);
    }
}
