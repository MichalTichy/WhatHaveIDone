using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;
public sealed record ImportBatch(ImportRun Next,List<ImportedProject> Projects,List<ImportedEntry> Entries);
