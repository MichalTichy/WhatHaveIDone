using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Pages;

public partial class Import : IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    private List<ImportModel> imports=[];
    private string token="",fileZone="Europe/Prague",sourceEmail="",previewId="";
    private bool loaded,fileMode,showHelpPending;
    private ElementReference helpDialog;
    private DotNetObjectReference<Import>? self;
    private IBrowserFile? file;
    private readonly HashSet<long> selectedWorkspaces=[];
    private readonly PeriodicTimer poll=new(TimeSpan.FromSeconds(2));
    private Task? polling;
    private ImportModel? Active=>imports.FirstOrDefault(r=>r.Status is "Queued" or "Running" or "Waiting" or "NeedsAttention" or "AwaitingConfirmation");
    protected override async Task OnInitializedAsync(){fileZone=User.Settings.TimeZone;await LoadAsync();polling=PollAsync();}
    private void ShowFileHelp() { fileMode = true; showHelpPending = true; }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!showHelpPending) return;
        showHelpPending = false; self ??= DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("whid.openDialog", Cancellation, helpDialog, self);
    }
    [JSInvokable] public async Task RequestCloseAsync() => await Js.InvokeVoidAsync("whid.closeDialog", Cancellation, helpDialog);
    private Task LoadAsync()=>RunAsync(async ct=>
    {
        var result=await Api.GetAsync<List<ImportModel>>("imports",ct);if(IsDisposed)return;imports=result;loaded=true;
        if(Active is {Status:"AwaitingConfirmation"} ready&&previewId!=ready.Id){previewId=ready.Id;selectedWorkspaces.Clear();foreach(var workspace in ready.Workspaces)selectedWorkspaces.Add(workspace.Id);sourceEmail=ready.AvailableEmails.Count==1?ready.AvailableEmails[0]:"";}
    });
    private async Task PollAsync()
    {
        try{while(await poll.WaitForNextTickAsync(Cancellation)){if(Active?.Status is "Queued" or "Running" or "Waiting")await InvokeAsync(LoadAsync);}}
        catch(OperationCanceledException)when(Cancellation.IsCancellationRequested){}
    }
    private void ToggleWorkspace(long id,ChangeEventArgs args){if(args.Value is true)selectedWorkspaces.Add(id);else selectedWorkspaces.Remove(id);}
    private Task PrepareAsync()=>RunAsync(async ct=>{await Api.SendAsync<string>(HttpMethod.Post,"imports/prepare",new PrepareApiImport(token),ct);token="";await LoadAsync();});
    private void SelectFile(InputFileChangeEventArgs args)=>file=args.File;
    private Task PrepareFileAsync()=>RunAsync(async ct=>
    {
        if(file is null)return;if(file.Size>20*1024*1024){Error=Strings.InvalidFile;return;}
        using var form=new MultipartFormDataContent();await using var stream=file.OpenReadStream(20*1024*1024,ct);form.Add(new StreamContent(stream),"file",file.Name);form.Add(new StringContent(fileZone),"timeZone");
        await Api.SendAsync<string>(HttpMethod.Post,"imports/file",form,ct);file=null;await LoadAsync();
    });
    private Task ConfirmAsync(ImportModel run)=>RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"imports/"+run.Id+"/confirm",new ConfirmImportInput(selectedWorkspaces.ToList(),sourceEmail),ct);await User.ReloadAsync(ct);await LoadAsync();});
    private Task SkipAsync()=>RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"onboarding/complete",null,ct);await User.ReloadAsync(ct);Navigation.NavigateTo("/time");});
    private Task ResumeAsync(ImportModel run)=>RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"imports/"+run.Id+"/resume",new PrepareApiImport(token),ct);token="";await LoadAsync();});
    private async Task CancelAsync(ImportModel run)
    {
        if(!await Js.InvokeAsync<bool>("confirm",Cancellation,Strings.CancelImportConfirm))return;
        await RunAsync(async ct=>{await Api.SendAsync<bool>(HttpMethod.Post,"imports/"+run.Id+"/cancel",null,ct);await LoadAsync();});
    }
    private static string Status(string status)=>Strings.ResourceManager.GetString(status)??Strings.Running;
    private static string Stage(string stage)=>stage switch{"Profile" or "VerifyProfile"=>Strings.Profile,"Workspaces"=>Strings.Workspaces,"Tags"=>Strings.Tags,"Projects"=>Strings.Projects,"Entries" or "File"=>Strings.Time,_=>Strings.History};
    public async ValueTask DisposeAsync(){Dispose();poll.Dispose();if(polling is not null)await polling;self?.Dispose();}
}
