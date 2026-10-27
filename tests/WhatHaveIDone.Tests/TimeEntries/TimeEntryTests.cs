using WhatHaveIDone.Models;

namespace WhatHaveIDone.Tests;

public sealed class TimeEntryTests
{
    private static TimeEntry New()=>new(){Id="entry",OwnerId="owner",CompatibilityId=1};
    [Fact] public void OptionalFieldsAndOffsetsArePreservedAsUtc()
    {
        var entry=New();entry.Change(null,null,[],DateTimeOffset.Parse("2026-10-26T09:00:00+01:00"),DateTimeOffset.Parse("2026-10-26T10:00:00+01:00"));
        Assert.Equal("",entry.Description);Assert.Null(entry.ProjectId);Assert.Equal(TimeSpan.Zero,entry.Start.Offset);Assert.Equal(8,entry.Start.Hour);
    }
    [Theory]
    [InlineData("2026-03-29T00:00:00+01:00","2026-03-30T00:00:00+02:00",23)]
    [InlineData("2026-10-25T00:00:00+02:00","2026-10-26T00:00:00+01:00",25)]
    public void LocalDayLengthUsesElapsedTime(string start,string end,int hours)
    {
        var entry=New();entry.Change("",null,[],DateTimeOffset.Parse(start),DateTimeOffset.Parse(end));
        Assert.Equal(hours*3600,entry.SecondsIn(entry.Start,entry.End!.Value,entry.End.Value));
    }
    [Fact] public void NegativeIntervalsFailButZeroDurationCanBeEdited()
    {
        var instant=DateTimeOffset.UnixEpoch;var entry=New();
        Assert.Throws<ArgumentException>(()=>entry.Change("",null,[],instant,instant.AddSeconds(-1)));
        entry.Change("zero",null,[],instant,instant);Assert.Equal("zero",entry.Description);Assert.Equal(0,entry.SecondsIn(instant,instant.AddDays(1),instant));
    }
    [Fact] public void SelectedPeriodClipsAnOvernightEntryWithoutSplittingIt()
    {
        var midnight=DateTimeOffset.Parse("2026-10-26T00:00:00Z");var entry=New();entry.Change("",null,[],midnight.AddHours(-1),midnight.AddHours(2));
        Assert.Equal(7200,entry.SecondsIn(midnight,midnight.AddDays(1),midnight));Assert.Equal(midnight.AddHours(-1),entry.Start);
    }
    [Fact] public void TagNormalizationIsCaseInsensitiveAndUnicodeAware()=>Assert.Equal(Tag.Normalize(" vývoj "),Tag.Normalize("VÝVOJ"));
}
