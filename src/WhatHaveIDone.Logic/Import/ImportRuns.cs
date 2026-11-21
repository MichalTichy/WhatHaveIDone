using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

internal static class ImportRuns
{
    public static void RequireLease(this ImportRun run, Guid lease)
    {
        if (run.Lease != lease || !run.Status.CanAdvance()) throw new AppException("Conflict", 409);
    }

    public static void Release(this ImportRun run)
    {
        run.Lease = null;
        run.LeaseUntil = null;
    }

    public static void Finish(this ImportRun run)
    {
        run.ProtectedToken = null;
        run.NextAttemptAt = null;
        run.TagNames.Clear();
        run.SourceProjects.Clear();
        run.Workspaces.Clear();
    }
}
