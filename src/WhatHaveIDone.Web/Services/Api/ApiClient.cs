using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Services.Api;

public sealed class ApiClient(HttpClient http)
{
    private const string BasePath = "api/v1/";
    private string? csrfToken;
    private string? language;

    public async Task<SessionModel> GetSessionAsync(CancellationToken ct)
    {
        var session = await GetAsync<SessionModel>("session", ct);
        csrfToken = session.CsrfToken;
        return session;
    }

    public void SetLanguage(string value) => language = value;

    public string SsoUrl => "/auth/sso";
    public Task<List<FeedbackModel>> GetFeedbackAsync(int skip, CancellationToken ct) => GetAsync<List<FeedbackModel>>($"feedback?skip={skip}", ct);
    public Task<string> SubmitFeedbackAsync(SubmitFeedback input, CancellationToken ct) => SendAsync<string>(HttpMethod.Post, "feedback", ct, input);
    public Task<string> RegisterAsync(AccountInput input, CancellationToken ct) => SendAsync<string>(HttpMethod.Post, "account/register", ct, input);
    public Task<string> LoginAsync(AccountInput input, CancellationToken ct) => SendAsync<string>(HttpMethod.Post, "account/login", ct, input);
    public Task<bool> LogoutAsync(CancellationToken ct) => SendAsync<bool>(HttpMethod.Post, "account/logout", ct);
    public Task<bool> ChangePasswordAsync(ChangePasswordInput input, CancellationToken ct) => SendAsync<bool>(HttpMethod.Post, "account/password", ct, input);

    public Task<SettingsModel> GetSettingsAsync(CancellationToken ct) => GetAsync<SettingsModel>("settings", ct);
    public Task<bool> SaveSettingsAsync(SettingsModel settings, CancellationToken ct) => SendAsync<bool>(HttpMethod.Put, "settings", ct, settings);
    public Task<bool> CompleteOnboardingAsync(CancellationToken ct) => SendAsync<bool>(HttpMethod.Post, "onboarding/complete", ct);

    public Task<List<ClientModel>> GetClientsAsync(CancellationToken ct) => GetAsync<List<ClientModel>>("clients", ct);
    public Task<bool> DeleteClientAsync(string id, Guid version, CancellationToken ct)
        => SendAsync<bool>(HttpMethod.Delete, $"clients/{Escape(id)}?version={version:D}", ct);

    public Task<string> SaveClientAsync(ClientModel client, CancellationToken ct)
        => string.IsNullOrEmpty(client.Id) ? SendAsync<string>(HttpMethod.Post, "clients", ct, client)
            : SendAsync<string>(HttpMethod.Put, $"clients/{Escape(client.Id)}", ct, client);

    public Task<Dictionary<string, double>> GetRecentProjectUsageAsync(CancellationToken ct)
        => GetAsync<Dictionary<string, double>>("projects/recent-usage", ct);

    public Task<List<ProjectModel>> GetProjectsAsync(CancellationToken ct) => GetAsync<List<ProjectModel>>("projects", ct);
    public Task<bool> DeleteProjectAsync(string id, Guid version, CancellationToken ct)
        => SendAsync<bool>(HttpMethod.Delete, $"projects/{Escape(id)}?version={version:D}", ct);

    public Task<string> SaveProjectAsync(ProjectModel project, CancellationToken ct)
        => string.IsNullOrEmpty(project.Id) ? SendAsync<string>(HttpMethod.Post, "projects", ct, project)
            : SendAsync<string>(HttpMethod.Put, $"projects/{Escape(project.Id)}", ct, project);
    public Task<List<string>> GetTagsAsync(CancellationToken ct) => GetAsync<List<string>>("tags", ct);
    public Task<List<string>> GetRecentTagsAsync(string projectId, CancellationToken ct)
        => GetAsync<List<string>>($"projects/{Escape(projectId)}/recent-tags", ct);

    public Task<List<TimeEntryModel>> GetTimeEntriesAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        => GetAsync<List<TimeEntryModel>>($"time-entries?{DateRange(from, to)}", ct);
    public Task<TimeEntryModel> GetTimeEntryAsync(string id, CancellationToken ct)
        => GetAsync<TimeEntryModel>($"time-entries/{Escape(id)}", ct);
    public Task<TimeEntryModel> SaveTimeEntryAsync(string? id, EditTimeEntry entry, CancellationToken ct)
        => id is null ? SendAsync<TimeEntryModel>(HttpMethod.Post, "time-entries", ct, entry)
            : SendAsync<TimeEntryModel>(HttpMethod.Put, $"time-entries/{Escape(id)}", ct, entry);
    public Task<string> DeleteTimeEntryAsync(string id, Guid version, CancellationToken ct)
        => SendAsync<string>(HttpMethod.Delete, $"time-entries/{Escape(id)}?version={version:D}", ct);
    public Task<TimeEntryModel?> GetTimerAsync(CancellationToken ct) => GetAsync<TimeEntryModel?>("timer", ct);
    public Task<TimeEntryModel> StartTimerAsync(EditTimeEntry entry, CancellationToken ct) => SendAsync<TimeEntryModel>(HttpMethod.Post, "timer/start", ct, entry);
    public Task<bool> StopTimerAsync(string id, CancellationToken ct) => SendAsync<bool>(HttpMethod.Post, $"timer/{Escape(id)}/stop", ct);

    public Task<ReportModel> GetReportAsync(DateTimeOffset from, DateTimeOffset to, string project, string tag, string text, CancellationToken ct)
        => GetAsync<ReportModel>($"reports?{ReportQuery(from, to, project, tag, text)}", ct);
    public string GetReportCsvUrl(DateTimeOffset from, DateTimeOffset to, string project, string tag, string text)
        => $"{BasePath}reports.csv?{ReportQuery(from, to, project, tag, text)}";

    public Task<List<ImportModel>> GetImportsAsync(CancellationToken ct) => GetAsync<List<ImportModel>>("imports", ct);
    public Task<string> PrepareImportAsync(PrepareApiImport input, CancellationToken ct) => SendAsync<string>(HttpMethod.Post, "imports/prepare", ct, input);
    public Task<bool> ConfirmImportAsync(string id, ConfirmImportInput input, CancellationToken ct)
        => SendAsync<bool>(HttpMethod.Post, $"imports/{Escape(id)}/confirm", ct, input);
    public Task<bool> ResumeImportAsync(string id, PrepareApiImport input, CancellationToken ct)
        => SendAsync<bool>(HttpMethod.Post, $"imports/{Escape(id)}/resume", ct, input);
    public Task<bool> CancelImportAsync(string id, CancellationToken ct)
        => SendAsync<bool>(HttpMethod.Post, $"imports/{Escape(id)}/cancel", ct);

    public Task<List<ApiTokenModel>> GetTokensAsync(CancellationToken ct) => GetAsync<List<ApiTokenModel>>("tokens", ct);
    public Task<CreatedApiToken> CreateTokenAsync(CreateApiKeyRequest input, CancellationToken ct)
        => SendAsync<CreatedApiToken>(HttpMethod.Post, "tokens", ct, input);
    public Task<bool> RevokeTokenAsync(string id, CancellationToken ct) => SendAsync<bool>(HttpMethod.Delete, $"tokens/{Escape(id)}", ct);

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string DateRange(DateTimeOffset from, DateTimeOffset to)
        => $"from={Escape(FormattableString.Invariant($"{from:O}"))}&to={Escape(FormattableString.Invariant($"{to:O}"))}";

    private static string ReportQuery(DateTimeOffset from, DateTimeOffset to, string project, string tag, string text)
        => $"{DateRange(from, to)}&project={Escape(project)}&tag={Escape(tag)}&text={Escape(text)}";

    private Task<T> GetAsync<T>(string path, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, path, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, CancellationToken cancellationToken, object? value = null)
    {
        using var request = new HttpRequestMessage(method, BasePath + path);
        if (value is not null) request.Content = JsonContent.Create(value, value.GetType());
        if (method != HttpMethod.Get && csrfToken is not null) request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        if (language is not null) request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        try { using var response = await http.SendAsync(request, cancellationToken); return await ReadAsync<T>(response, cancellationToken); }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested) { throw new HttpRequestException(Strings.Offline, exception); }
    }
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken); } catch (JsonException) { }
            throw new ApiCallException(error?.Code ?? "UnexpectedError", error?.Message ?? Strings.UnexpectedError, (int)response.StatusCode);
        }
        if (response.StatusCode == HttpStatusCode.NoContent) return default!;
        try { return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!; }
        catch (JsonException) { throw new ApiCallException("UnexpectedError", Strings.UnexpectedError, (int)response.StatusCode); }
    }
}
