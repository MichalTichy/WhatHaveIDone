namespace WhatHaveIDone.Api.Tests;
public sealed class MutableTimeProvider : TimeProvider
{
    // Match the dated fixtures and avoid sub-microsecond rounding at PostgreSQL retry boundaries.
    private DateTimeOffset now=new(2026,10,26,12,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow()=>now;
    public void Advance(TimeSpan duration)=>now+=duration;
}
