using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Logic.Authentication;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Infrastructure.Identity;

public sealed class AccountAccess(UserManager<AppUser> users, SignInManager<AppUser> signIn, IdentityDbContext database, IHttpContextAccessor http, IConfiguration configuration) : IAccountAccess
{
    public async Task<string> RegisterAsync(AccountInput input, CancellationToken cancellationToken)
    {
        if (!Validator.TryValidateObject(input, new ValidationContext(input), [], true)) throw new AppException("InvalidAccount");
        var user = new AppUser { Email = input.Email.Trim(), UserName = input.Email.Trim(), Name = input.Email.Trim(), EmailConfirmed = false };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded) throw new AppException("InvalidAccount");
        cancellationToken.ThrowIfCancellationRequested(); return user.Id;
    }

    public async Task<string> SignInAsync(AccountInput input, CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(input.Email)||string.IsNullOrEmpty(input.Password)||input.Email.Length>254||input.Password.Length>128)throw new AppException("InvalidLogin",401);
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null || !(await signIn.CheckPasswordSignInAsync(user, input.Password, true)).Succeeded) throw new AppException("InvalidLogin", 401);
        cancellationToken.ThrowIfCancellationRequested(); return user.Id;
    }

    public async Task<string> SignInExternalAsync(string tenant, string objectId, string email, string name, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Sso:Enabled") || !string.Equals(tenant, configuration["AzureAd:TenantId"], StringComparison.OrdinalIgnoreCase)) throw new AppException("SsoDenied", 403);
        var key = tenant.ToLowerInvariant() + ":" + objectId.ToLowerInvariant();
        var user = await users.FindByLoginAsync("Entra", key);
        if (user is null)
        {
            if (!new EmailAddressAttribute().IsValid(email) || await users.FindByEmailAsync(email) is not null) throw new AppException("SsoAccountConflict", 409);
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            user = new AppUser { Email = email, UserName = email, Name = name, EmailConfirmed = false };
            if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddLoginAsync(user, new UserLoginInfo("Entra", key, "Microsoft"))).Succeeded) throw new AppException("SsoDenied", 403);
            await transaction.CommitAsync(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested(); return user.Id;
    }

    public async Task IssueSessionAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.Id==id,cancellationToken) ?? throw new AppException("InvalidLogin",401);
        if (!await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user)) throw new AppException("InvalidLogin", 401);
        await signIn.SignInAsync(user, isPersistent: false);
        http.HttpContext!.User = await signIn.CreateUserPrincipalAsync(user);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); await signIn.SignOutAsync(); }
    public async Task ChangePasswordAsync(ChangePasswordInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.GetUserAsync(http.HttpContext!.User) ?? throw new AppException("Unauthenticated", 401);
        if (string.IsNullOrEmpty(input.CurrentPassword) || string.IsNullOrEmpty(input.NewPassword) || input.NewPassword.Length > 128 || !(await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword)).Succeeded) throw new AppException("InvalidPassword");
        await signIn.RefreshSignInAsync(user);
    }
}
