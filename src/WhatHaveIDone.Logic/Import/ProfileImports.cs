using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public static class ProfileImports
{
    public static ImportRun RequireImport(this UserProfile profile, string id)
        => (profile.Import?.Id == id ? profile.Import : profile.ImportHistory.FirstOrDefault(r => r.Id == id))
           ?? throw new AppException("NotFound", 404);

    public static void Finish(this ImportRun run)
    {
        run.ProtectedToken = null;
        run.FilePath = null;
        run.TagNames.Clear();
        run.Preview.Clear();
        run.Workspaces.Clear();
        run.AvailableEmails.Clear();
    }
}
