using FastEndpoints;
using System.Globalization;
using System.Text;
using CsvHelper;
using WhatHaveIDone.Logic.Reporting;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ExportReportEndpoint(ReportQuery query) : Endpoint<TimeRange, object>
{
    public override void Configure() => Get("/api/v1/reports.csv");

    public override async Task HandleAsync(TimeRange request, CancellationToken ct)
    {
        var report = await query.RunAsync(request.From, request.To, request.Project, request.Tag, request.Text, ct);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var header in new[] { "ID", "Description", "Project ID", "Start UTC", "End UTC", "Seconds in selected period", "Tags" }) csv.WriteField(header);
            await csv.NextRecordAsync();
            foreach (var entry in report.Entries)
            {
                ct.ThrowIfCancellationRequested();
                csv.WriteField(entry.CompatibilityId); csv.WriteField(entry.Description); csv.WriteField(entry.ProjectId);
                csv.WriteField(entry.Start.ToString("O")); csv.WriteField(entry.End?.ToString("O"));
                csv.WriteField(ReportQuery.Seconds(entry, request.From, request.To, report.CalculatedAt)); csv.WriteField(string.Join(", ", entry.Tags));
                await csv.NextRecordAsync();
            }
        }
        await Send.ResultAsync(Results.File(Encoding.UTF8.GetBytes("\uFEFF" + writer), "text/csv; charset=utf-8", "time-entries.csv"));
    }
}
