using MediatR;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Authentication;

public sealed record RegisterAccountUsecase(AccountInput Input) : IRequest<string>;

public sealed class RegisterAccountUsecaseHandler(IAccountAccess accounts, AccountProfileProvisioner profiles) : UsecaseHandlerBase<RegisterAccountUsecase, string>
{
    protected override Task ValidateAccessAsync(RegisterAccountUsecase request, CancellationToken cancellationToken) => Task.CompletedTask; // Public registration is intentional.
    protected override async Task<string> ExecuteAsync(RegisterAccountUsecase request, CancellationToken cancellationToken)
    {
        var id = await accounts.RegisterAsync(request.Input, cancellationToken);
        await profiles.EnsureAsync(id, cancellationToken);
        return id;
    }
}
