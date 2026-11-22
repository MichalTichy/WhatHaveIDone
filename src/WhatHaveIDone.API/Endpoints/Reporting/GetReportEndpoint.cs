using FastEndpoints;
using FastEndpoints.Mcp;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Reporting;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class GetReportEndpoint(ReportQuery query) : Endpoint<TimeRange, ReportModel>
{
    public override void Configure()
    {
        Get("/api/v1/reports");
        this.McpTool("get_report", "Summarize entries in [from, to) with totals by project and local day. Durations are clipped to the interval.", tool =>
        {
            tool.Hints.ReadOnly = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(TimeRange request, CancellationToken ct)
    {
        await Send.OkAsync(await query.RunAsync(request.From, request.To, request.Project, request.Tag, request.Text, ct), ct);
    }
}
