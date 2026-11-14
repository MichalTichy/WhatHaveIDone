using System.Globalization;
using System.Text;
using CsvHelper;
using WhatHaveIDone.Logic.Reporting;

namespace WhatHaveIDone.API.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/reports", async (DateTimeOffset from, DateTimeOffset to, string? project, string? tag, string? text, ReportQuery query, CancellationToken ct) => await query.RunAsync(from, to, project, tag, text, ct)).RequireAuthorization();
        app.MapGet("/api/v1/reports.csv", async (DateTimeOffset from, DateTimeOffset to, string? project, string? tag, string? text, ReportQuery query, CancellationToken ct) =>
        {
            var report = await query.RunAsync(from, to, project, tag, text, ct);
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
                    csv.WriteField(ReportQuery.Seconds(entry, from, to, report.CalculatedAt)); csv.WriteField(string.Join(", ", entry.Tags));
                    await csv.NextRecordAsync();
                }
            }
            return Results.File(Encoding.UTF8.GetBytes("\uFEFF" + writer), "text/csv; charset=utf-8", "time-entries.csv");
        }).RequireAuthorization();
    }
}
