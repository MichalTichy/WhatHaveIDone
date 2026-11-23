using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;

public sealed class TogglImportClient(HttpClient http, IDataProtectionProvider protection, TimeProvider clock, ILogger<TogglImportClient> logger)
{
    public const string ProtectionPurpose = "WhatHaveIDone.TogglImport.v1";

    public async Task<TogglImportSource> GetSourceAsync(string token, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(token, HttpMethod.Get, "api/v9/me?with_related_data=true", null, 0, cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;
        var userId = Long(root, "id") ?? throw new TogglImportException("ImportSource");
        var zone = Text(root, "timezone");
        if (string.IsNullOrWhiteSpace(zone)) zone = "UTC";
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)) throw new TogglImportException("ImportSource");

        var workspaces = Related(root, "workspaces").Select(w => new ImportWorkspace(
            Long(w, "id") ?? throw new TogglImportException("ImportSource"), Text(w, "name"))).DistinctBy(w => w.Id).ToList();
        var projects = Related(root, "projects").Select(p => new ImportedProject(
            Long(p, "id") ?? throw new TogglImportException("ImportSource"), Text(p, "name"), Text(p, "color"),
            p.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.False))
            .DistinctBy(p => p.OriginalId).ToDictionary(p => p.OriginalId);
        var tags = Related(root, "tags").Where(t => !string.IsNullOrWhiteSpace(Text(t, "name")))
            .Select(t => (Id: Long(t, "id") ?? throw new TogglImportException("ImportSource"), Name: Text(t, "name")))
            .DistinctBy(t => t.Id).ToDictionary(t => t.Id, t => t.Name);

        return new TogglImportSource(userId, Text(root, "email"), zone, workspaces, projects, tags);
    }

    public async Task<ImportBatch> FetchAsync(ImportRun run, CancellationToken cancellationToken)
    {
        var call = run.PendingCalls.FirstOrDefault()
            ?? throw new TogglImportException("ImportSource");
        // Runs queued before windowing may still have an unbounded first request.
        if (call.FirstId is null && call.FirstRow is null)
            call = ImportCall.CreateWindow(call.WorkspaceId, call.From, call.Through);
        var protector = protection.CreateProtector(ProtectionPurpose);
        var token = protector.Unprotect(run.ProtectedToken ?? throw new TogglImportException("ImportToken"));
        var body = new Dictionary<string, object?>
        {
            ["start_date"] = call.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["end_date"] = call.Through.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["user_ids"] = new[] { run.SourceUserId },
            ["page_size"] = 1000,
            ["order_by"] = "date",
            ["order_dir"] = "DESC",
            ["grouped"] = false,
            ["enrich_response"] = true,
            // The live Reports API accepts 0 for unrounded entries and rejects booleans.
            ["rounding"] = 0
        };
        if (call.FirstId is { } firstId) body["first_id"] = firstId;
        if (call.FirstRow is { } firstRow) body["first_row_number"] = firstRow;

        using var response = await SendAsync(token, HttpMethod.Post,
            $"reports/api/v3/workspace/{call.WorkspaceId}/search/time_entries", body, run.RetryCount, cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        List<ImportedEntry> entries = [];
        foreach (var row in Rows(json.RootElement))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceUser = Long(row, "user_id") ?? Long(row, "uid") ?? run.SourceUserId;
            var projectId = Long(row, "project_id") ?? Long(row, "pid");
            if (projectId <= 0) projectId = null;
            var tagNames = ReadTags(row, run.TagNames, out var missingTag);
            var items = row.TryGetProperty("time_entries", out var nested) && nested.ValueKind == JsonValueKind.Array
                ? nested.EnumerateArray().ToArray()
                : [row];
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Long(item, "id") is not (> 0 and var id)) throw new TogglImportException("ImportSource");
                if (!DateTimeOffset.TryParse(Text(item, "start"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                    throw new TogglImportException("ImportSource");
                DateTimeOffset? end = null;
                var stop = Text(item, "stop");
                if (stop.Length > 0) end = DateTimeOffset.Parse(stop, CultureInfo.InvariantCulture);
                else if (!item.TryGetProperty("stop", out _) && Long(item, "seconds") is >= 0 and var seconds)
                    end = start.AddSeconds(seconds);
                entries.Add(new ImportedEntry(id, Text(row, "description"), projectId,
                    Text(row, "project_name"), tagNames, start.ToUniversalTime(), end?.ToUniversalTime(),
                    SourceUserId: Long(item, "user_id") ?? Long(item, "uid") ?? sourceUser, ErrorCode: missingTag ? "ImportSource" : null));
            }
        }

        var nextId = HeaderLong(response, "X-Next-ID");
        var nextRow = checked((int?)HeaderLong(response, "X-Next-Row-Number"));
        ImportCall? continuation = null;
        if (nextId.HasValue || nextRow.HasValue)
        {
            continuation = call with { FirstId = nextId, FirstRow = nextRow };
            if (continuation == call || run.ConsumedCursors.Contains(continuation.CursorKey()))
                throw new TogglImportException("ImportSource");
        }
        else if (run.RequestedFrom is { } from && call.From > from)
        {
            continuation = ImportCall.CreateWindow(call.WorkspaceId, from, call.From.AddDays(-1));
        }
        return new ImportBatch(entries, continuation);
    }

    private static List<string> ReadTags(JsonElement row, IReadOnlyDictionary<long, string> sourceTags, out bool missingTag)
    {
        List<string> names = [];
        missingTag = false;
        if (row.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tags.EnumerateArray())
            {
                var name = tag.ValueKind == JsonValueKind.String ? tag.GetString() : Text(tag, "name");
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
        }
        if (names.Count == 0 && row.TryGetProperty("tag_ids", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in ids.EnumerateArray())
            {
                if (id.TryGetInt64(out var value) && sourceTags.TryGetValue(value, out var name)) names.Add(name);
                else missingTag = true;
            }
        }
        return names;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string path, object? body,
        int retryCount, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":api_token")));
        request.Headers.UserAgent.ParseAdd("WhatHaveIDone/1.0");
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var quotaReset = HeaderLong(response, "X-Toggl-Quota-Resets-In");
            var rateReset = HeaderLong(response, "X-RateLimit-Reset") ?? HeaderLong(response, "RateLimit-Reset");
            var quota = response.StatusCode == HttpStatusCode.TooManyRequests
                || response.StatusCode == HttpStatusCode.PaymentRequired
                && (response.Headers.RetryAfter is not null || quotaReset.HasValue || rateReset.HasValue
                    || new[] { "quota", "rate limit", "api limit", "limit exceeded", "too many requests" }
                        .Any(message => payload.Contains(message, StringComparison.OrdinalIgnoreCase)));

            if (quota)
            {
                var now = clock.GetUtcNow();
                var next = response.Headers.RetryAfter?.Date
                    ?? (response.Headers.RetryAfter?.Delta is { } delta ? now + delta : (DateTimeOffset?)null);
                if (quotaReset is > 0 and <= 86400) next = Later(next, now.AddSeconds(quotaReset.Value));
                if (rateReset is { } reset && reset > now.ToUnixTimeSeconds() && reset <= now.AddDays(1).ToUnixTimeSeconds())
                    next = Later(next, DateTimeOffset.FromUnixTimeSeconds(reset));
                next ??= now.AddSeconds(Math.Min(3600, 60 * Math.Pow(2, Math.Min(retryCount, 6))));
                logger.LogWarning("Toggl {Operation} returned HTTP {StatusCode} (ImportQuota). Retry after {RetryAt}.",
                    body is null ? "source lookup" : "detailed report", (int)response.StatusCode, next);
                throw new TogglImportException("ImportQuota", next > now ? next : now.AddSeconds(1));
            }

            var code = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "ImportToken",
                HttpStatusCode.PaymentRequired => "ImportPlan",
                _ => "ImportSource"
            };
            var mentionedFields = new[] { "rounding", "start_date", "end_date", "page_size", "order_by", "order_dir",
                "user_ids", "first_id", "first_row_number", "enrich_response", "grouped", "workspace_id" }
                .Where(field => payload.Contains(field, StringComparison.OrdinalIgnoreCase));
            var reason = payload.Contains("Maximum allowed date range", StringComparison.OrdinalIgnoreCase)
                ? "date range too wide" : "upstream request rejected";
            logger.LogWarning("Toggl {Operation} returned HTTP {StatusCode} ({ErrorCode}): {Reason}. Report parameters mentioned: {Parameters}.",
                body is null ? "source lookup" : "detailed report", (int)response.StatusCode, code, reason, string.Join(", ", mentionedFields));
            throw new TogglImportException(code);
        }
    }

    private static DateTimeOffset Later(DateTimeOffset? current, DateTimeOffset candidate)
        => current is { } value && value > candidate ? value : candidate;

    private static IEnumerable<JsonElement> Related(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [];

    private static IEnumerable<JsonElement> Rows(JsonElement root)
    {
        if(root.ValueKind==JsonValueKind.Array)return root.EnumerateArray().ToArray();
        if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("items",out var items)&&items.ValueKind==JsonValueKind.Array)return items.EnumerateArray().ToArray();
        if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("data",out var data)&&data.ValueKind==JsonValueKind.Array)return data.EnumerateArray().ToArray();
        throw new TogglImportException("ImportSource");
    }
    private static string Text(JsonElement value,string name)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(name,out var found)&&found.ValueKind==JsonValueKind.String?found.GetString()??"":"";
    private static long? Long(JsonElement value,string name)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(name,out var found)&&long.TryParse(found.ToString(),CultureInfo.InvariantCulture,out var number)?number:null;
    private static long? HeaderLong(HttpResponseMessage response,string name)=>response.Headers.TryGetValues(name,out var values)&&long.TryParse(values.FirstOrDefault(),CultureInfo.InvariantCulture,out var result)?result:null;
}
