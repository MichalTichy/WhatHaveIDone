using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Authentication;

public sealed record SignInUsecase(AccountInput Input) : IRequest<string>;

public sealed class SignInUsecaseHandler(IAccountAccess accounts, AccountProfileProvisioner profiles) : UsecaseHandlerBase<SignInUsecase, string>
{
    protected override Task ValidateAccessAsync(SignInUsecase request, CancellationToken cancellationToken) => Task.CompletedTask; // The account service validates credentials before returning an owner.
    protected override async Task<string> ExecuteAsync(SignInUsecase request, CancellationToken cancellationToken)
    {
        var id = await accounts.SignInAsync(request.Input, cancellationToken);
        await profiles.EnsureAsync(id, cancellationToken);
        return id;
    }
}
