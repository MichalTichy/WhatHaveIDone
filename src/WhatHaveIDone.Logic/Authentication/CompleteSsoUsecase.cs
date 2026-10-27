using MediatR;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Authentication;

public sealed record CompleteSsoUsecase(string Tenant, string ObjectId, string Email, string Name) : IRequest<string>;

public sealed class CompleteSsoUsecaseHandler(IAccountAccess accounts, AccountProfileProvisioner profiles) : UsecaseHandlerBase<CompleteSsoUsecase, string>
{
    // Only the validated provider callback constructs this request; no public endpoint accepts these claims.
    protected override Task ValidateAccessAsync(CompleteSsoUsecase request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.Tenant, out _) || !Guid.TryParse(request.ObjectId, out _)) throw new AppException("SsoDenied", 403);
        return Task.CompletedTask;
    }
    protected override async Task<string> ExecuteAsync(CompleteSsoUsecase request, CancellationToken cancellationToken)
    {
        var id = await accounts.SignInExternalAsync(request.Tenant, request.ObjectId, request.Email, request.Name, cancellationToken);
        await profiles.EnsureAsync(id, cancellationToken);
        return id;
    }
}
