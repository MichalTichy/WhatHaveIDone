namespace WhatHaveIDone.Contracts;
public sealed record ReportModel(DateTimeOffset CalculatedAt, double TotalSeconds, List<ReportBucket> Projects, List<ReportBucket> Days, List<TimeEntryModel> Entries);
