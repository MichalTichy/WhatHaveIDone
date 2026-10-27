using WhatHaveIDone.Contracts;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Pages;

public partial class Account
{
    private readonly AccountInput input = new();
    private string confirmation = "";
    private bool register;
    protected override void OnParametersSet()
    {
        register = Navigation.ToBaseRelativePath(Navigation.Uri).StartsWith("register", StringComparison.Ordinal);
        if (User.Session.Authenticated) Navigation.NavigateTo("/time");
        if (Navigation.Uri.Contains("error=", StringComparison.Ordinal)) Error = Strings.SsoDenied;
    }
    private Task SubmitAsync() => RunAsync(async ct =>
    {
        if (register && confirmation != input.Password) { Error = Strings.PasswordsDiffer; return; }
        await Api.SendAsync<string>(HttpMethod.Post, register ? "account/register" : "account/login", input, ct);
        Navigation.NavigateTo("/time", forceLoad: true);
    });
}
