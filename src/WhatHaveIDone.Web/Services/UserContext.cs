using System.Globalization;
using System.Net.Http.Headers;
using WhatHaveIDone.Contracts;

namespace WhatHaveIDone.Web.Services;

public sealed class UserContext(ApiClient api)
{
    private int reloadGeneration;
    public SessionModel Session { get; private set; } = new(false, null, false, false, null);
    public SettingsModel Settings { get; set; } = new();
    public List<ClientModel> Clients { get; private set; } = [];
    public List<ProjectModel> Projects { get; private set; } = [];
    public List<TagModel> Tags { get; private set; } = [];
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Session = await api.GetAsync<SessionModel>("session", cancellationToken);
        api.CsrfToken = Session.CsrfToken;
        if (Session.Authenticated) await ReloadAsync(cancellationToken);
        else { Clients = []; Projects = []; Tags = []; Settings = new(); }
    }
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var generation = ++reloadGeneration;
        var settings = await api.GetAsync<SettingsModel>("settings", cancellationToken);
        var clients = await api.GetAsync<List<ClientModel>>("clients", cancellationToken);
        var projects = await api.GetAsync<List<ProjectModel>>("projects", cancellationToken);
        var tags = await api.GetAsync<List<TagModel>>("tags", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != reloadGeneration) return;
        Settings = settings; Clients = clients; Projects = projects; Tags = tags;
        var culture = CultureInfo.GetCultureInfo(Settings.Language);
        CultureInfo.DefaultThreadCurrentCulture = culture; CultureInfo.DefaultThreadCurrentUICulture = culture;
        api.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        api.Http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(Settings.Language));
    }
    public string? DefaultClientId => Clients.FirstOrDefault(c => c.IsDefault)?.Id;
    public string ClientName(string? id) => Clients.FirstOrDefault(c => c.Id == id)?.Name ?? "";
    public string ProjectName(string? id) => Projects.FirstOrDefault(p => p.Id == id)?.Name ?? Resources.Strings.NoProject;
    public string ProjectColor(string? id) => Projects.FirstOrDefault(p => p.Id == id)?.Color ?? "#687483";
}
