using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Logic.Authentication;

public interface IAccountAccess
{
    Task<string> RegisterAsync(AccountInput input, CancellationToken cancellationToken);
    Task<string> SignInAsync(AccountInput input, CancellationToken cancellationToken);
    Task<string> SignInExternalAsync(string tenant, string objectId, string email, string name, CancellationToken cancellationToken);
    Task IssueSessionAsync(string id, CancellationToken cancellationToken);
    Task SignOutAsync(CancellationToken cancellationToken);
    Task ChangePasswordAsync(ChangePasswordInput input, CancellationToken cancellationToken);
}
