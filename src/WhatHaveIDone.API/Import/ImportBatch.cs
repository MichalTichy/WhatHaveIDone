using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;

public sealed record ImportBatch(List<ImportedEntry> Entries, ImportCall? Continuation = null);
