using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Predictions;

public sealed record SaveWorkdayOverrideUsecase(SaveWorkdayRequest Input) : IRequest<bool>;

public sealed class SaveWorkdayOverrideUsecaseHandler(IRepository<WorkCalendarMonth> calendars)
    : UsecaseHandlerBase<SaveWorkdayOverrideUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(SaveWorkdayOverrideUsecase request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        WorkCalendarRules.ValidateMonth(input.Date.Year, input.Date.Month);
        if (input.Percent.HasValue && input.Percent is not (0 or 25 or 50 or 75 or 100))
        {
            throw new AppException("InvalidRequest");
        }
        var id = WorkCalendarRules.MonthId(input.Date);
        var month = await calendars.GetByIdAsync(id, cancellationToken);
        var isNew = month is null;
        if (month is null)
        {
            if (input.Version != Guid.Empty)
            {
                throw new AppException("Conflict", 409);
            }
            month = new WorkCalendarMonth { Id = id };
        }
        else
        {
            Documents.CheckVersion(month, input.Version);
        }
        if (input.Percent.HasValue)
        {
            month.Overrides[input.Date.Day] = input.Percent.Value;
        }
        else
        {
            month.Overrides.Remove(input.Date.Day);
        }
        if (isNew)
        {
            await calendars.AddAsync(month, cancellationToken);
        }
        else
        {
            await calendars.UpdateAsync(month, cancellationToken);
        }
        return true;
    }
}
