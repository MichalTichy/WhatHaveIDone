using Microsoft.AspNetCore.Components;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Time;

public partial class MonthlyPrediction
{
    [Parameter] public int Revision { get; set; }
    private int loadedRevision = -1;
    private MonthlyPredictionModel? prediction;

    protected override void OnParametersSet()
    {
        if (loadedRevision != Revision)
        {
            loadedRevision = Revision;
            // Do not join the parent's render/load task: the time list must stay independent.
            _ = LoadAsync();
        }
    }

    private Task LoadAsync() => ExecuteLoadAsync(Api.GetMonthlyPredictionAsync, result => prediction = result);

    private static string Hours(double seconds)
    {
        var minutes = (long)Math.Round(seconds / 60);
        return string.Format(Strings.PredictionHours, minutes / 60, minutes % 60);
    }
}
