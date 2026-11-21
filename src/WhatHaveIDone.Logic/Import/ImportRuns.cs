using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

internal static class ImportRuns
{
    public static void Finish(this ImportRun run)
    {
        run.ProtectedToken = null;
        run.NextAttemptAt = null;
        run.TagNames.Clear();
        run.SourceProjects.Clear();
        run.Workspaces.Clear();
        run.PendingCalls.Clear();
        run.ConsumedCursors.Clear();
    }
}
