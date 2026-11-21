using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Features.Import;

public partial class Import : IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    private List<ImportModel> imports=[];
    private string token="",previewId="";
    private DateOnly? from, through;
    private bool loaded;
    private readonly HashSet<long> selectedWorkspaces=[];
    private readonly PeriodicTimer poll=new(TimeSpan.FromSeconds(2));
    private Task? polling;
    private ImportModel? Active=>imports.FirstOrDefault(r=>r.Status.IsActive());
    protected override async Task OnInitializedAsync(){await LoadAsync();polling=PollAsync();}
    private Task LoadAsync()=>RunAsync(async ct=>
    {
        var result=await Api.GetAsync<List<ImportModel>>("imports",ct);if(IsDisposed)return;imports=result;loaded=true;
        if(Active is {Status:ImportStatus.AwaitingConfirmation} ready&&previewId!=ready.Id){previewId=ready.Id;selectedWorkspaces.Clear();foreach(var workspace in ready.Workspaces)selectedWorkspaces.Add(workspace.Id);}
    });
    private async Task PollAsync()
    {
        try{while(await poll.WaitForNextTickAsync(Cancellation)){if(Active?.Status.CanAdvance() == true)await InvokeAsync(LoadAsync);}}
        catch(OperationCanceledException)when(Cancellation.IsCancellationRequested){}
    }
    private void ToggleWorkspace(long id,ChangeEventArgs args){if(args.Value is true)selectedWorkspaces.Add(id);else selectedWorkspaces.Remove(id);}
    private Task PrepareAsync()=>RunAsync(async ct=>{await Api.SendAsync<string>(HttpMethod.Post,"imports/prepare",new PrepareApiImport(token, from, through),ct);token="";await LoadAsync();});
    private Task ConfirmAsync(ImportModel run)=>RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"imports/"+run.Id+"/confirm",new ConfirmImportInput(selectedWorkspaces.ToList()),ct);await User.ReloadAsync(ct);await LoadAsync();});
    private Task SkipAsync()=>RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"onboarding/complete",null,ct);await User.ReloadAsync(ct);Navigation.NavigateTo("/time");});
    private Task ResumeAsync(ImportModel run)=>RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"imports/"+run.Id+"/resume",new PrepareApiImport(token),ct);token="";await LoadAsync();});
    private async Task CancelAsync(ImportModel run)
    {
        if(!await Js.InvokeAsync<bool>("confirm",Cancellation,Strings.CancelImportConfirm))return;
        await RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"imports/"+run.Id+"/cancel",null,ct);await LoadAsync();});
    }
    private string ImportedRange(ImportModel run)
        => $"{UserTime.Local(run.RangeStart!.Value, User.Settings.TimeZone):d} – {UserTime.Local(run.RangeEnd!.Value, User.Settings.TimeZone):d}";
    private static string Status(ImportStatus status)=>Strings.ResourceManager.GetString(status.ToString())??Strings.Running;
    public async ValueTask DisposeAsync(){Dispose();poll.Dispose();if(polling is not null)await polling;}
}
