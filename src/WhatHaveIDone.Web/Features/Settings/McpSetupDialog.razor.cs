using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class McpSetupDialog
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Parameter, EditorRequired] public string Endpoint { get; set; } = "";
    [Parameter] public EventCallback Closed { get; set; }
    private ElementReference dialog;
    private readonly ElementReference[] tabElements = new ElementReference[4];
    private DotNetObjectReference<McpSetupDialog>? self;
    private int method;
    private bool copied;
    private string[] Methods => [Strings.McpAiPrompt, "VS Code", "Cursor", "Claude Code"];
    private string MethodHelp => method switch { 1 => Strings.McpVsCodeHelp, 2 => Strings.McpCursorHelp, 3 => Strings.McpClaudeHelp, _ => Strings.McpPromptHelp };
    private string? DocumentationUrl => method switch
    {
        1 => "https://code.visualstudio.com/docs/agents/reference/mcp-configuration",
        2 => "https://cursor.com/docs/mcp",
        3 => "https://code.claude.com/docs/en/mcp",
        _ => null
    };
    private string Configuration
    {
        get
        {
            if (method == 0) return string.Format(Strings.McpSetupPrompt, Endpoint);
            var server = new Dictionary<string, object>
            {
                ["url"] = Endpoint,
                ["headers"] = new Dictionary<string, string> { ["Authorization"] = method == 3 ? "Bearer ${WHID_API_KEY}" : "Bearer ${env:WHID_API_KEY}" }
            };
            if (method != 2) server["type"] = "http";
            var configuration = new Dictionary<string, object>
            {
                [method == 1 ? "servers" : "mcpServers"] = new Dictionary<string, object> { ["what-have-i-done"] = server }
            };
            return JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        self = DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("whid.openDialog", Cancellation, dialog, self);
    }
    private async Task TabKeyDownAsync(KeyboardEventArgs args, int index)
    {
        var next = args.Key switch { "ArrowRight" => (index + 1) % 4, "ArrowLeft" => (index + 3) % 4, "Home" => 0, "End" => 3, _ => -1 };
        if (next < 0) return;
        SelectMethod(next);
        await tabElements[next].FocusAsync(preventScroll: true);
    }
    private void SelectMethod(int index) { method = index; copied = false; Error = null; }
    private async Task CopyConfigurationAsync()
    {
        copied = false;
        var copiedMethod = method;
        await RunAsync(async ct =>
        {
            try { await Js.InvokeVoidAsync("whid.copy", ct, Configuration); if (!IsDisposed && method == copiedMethod) copied = true; }
            catch (JSException) { if (!IsDisposed) Error = Strings.McpCopyFailed; }
        });
    }
    [JSInvokable]
    public async Task RequestCloseAsync()
    {
        await Js.InvokeVoidAsync("whid.closeDialog", Cancellation, dialog);
        await Closed.InvokeAsync();
    }
    public override void Dispose() { self?.Dispose(); base.Dispose(); }
}
