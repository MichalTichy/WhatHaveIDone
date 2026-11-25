using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class Prediction
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    private DateOnly month, today;
    private WorkCalendarModel? calendar;
    private WorkdayModel? editingDay;
    private int editPercent;
    private ElementReference slider;
    private ElementReference dialog;
    private DotNetObjectReference<Prediction>? self;
    private bool focusSlider;
    private bool enabled;

    private string MonthLabel => month.ToString("MMMM yyyy");
    private int LeadingDays => ((int)month.DayOfWeek - User.Settings.FirstDayOfWeek + 7) % 7;
    private IEnumerable<string> WeekdayLabels => Enumerable.Range(0, 7)
        .Select(i => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((User.Settings.FirstDayOfWeek + i) % 7)));

    protected override async Task OnInitializedAsync()
    {
        today = DateOnly.FromDateTime(UserTime.Local(DateTimeOffset.UtcNow, User.Settings.TimeZone));
        month = new DateOnly(today.Year, today.Month, 1);
        enabled = User.Settings.PredictionEnabled;
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (focusSlider)
        {
            focusSlider = false;
            self ??= DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("whid.openDialog", Cancellation, dialog, self);
            await slider.FocusAsync();
        }
    }

    private Task LoadAsync()
    {
        var selected = month;
        return ExecuteLoadAsync(ct => Api.GetWorkCalendarAsync(selected.Year, selected.Month, ct), result => calendar = result);
    }

    private Task ReloadAsync() => RunAsync(async ct =>
    {
        await User.ReloadAsync(ct);
        enabled = User.Settings.PredictionEnabled;
        await LoadAsync();
    });

    private Task MoveMonthAsync(int offset)
    {
        month = month.AddMonths(offset);
        calendar = null;
        return LoadAsync();
    }

    private async Task ToggleAsync()
    {
        await RunAsync(async ct =>
        {
            var result = await Api.SavePredictionSettingsAsync(new PredictionSettingsModel(enabled, User.Settings.Version), ct);
            User.Settings = User.Settings with { PredictionEnabled = result.Enabled, Version = result.Version };
            Feedback.Show(Strings.Saved);
        });
        enabled = User.Settings.PredictionEnabled;
    }

    private void Edit(WorkdayModel day)
    {
        Error = null;
        editingDay = day;
        editPercent = day.Percent;
        focusSlider = true;
    }

    [JSInvokable]
    public async Task RequestCloseAsync()
    {
        if (Busy)
        {
            return;
        }
        await Js.InvokeVoidAsync("whid.closeDialog", Cancellation, dialog);
        editingDay = null;
        Error = null;
        StateHasChanged();
    }

    private Task SaveDayAsync(bool restore) => RunAsync(async ct =>
    {
        if (editingDay is null || calendar is null)
        {
            return;
        }
        await Api.SaveWorkdayAsync(new SaveWorkdayRequest(editingDay.Date, restore ? null : editPercent, calendar.Version), ct);
        await Js.InvokeVoidAsync("whid.closeDialog", ct, dialog);
        editingDay = null;
        await LoadAsync();
        Feedback.Show(Strings.Saved);
    });

    private static string DayLabel(WorkdayModel day) => $"{day.Date:D}, {day.Percent} %"
        + (day.Holiday is null ? "" : $", {HolidayName(day.Holiday)}")
        + (day.IsOverride ? $", {Strings.CustomCapacity}" : "");

    private static string HolidayName(string holiday) => holiday switch
    {
        "NewYear" => Strings.HolidayNewYear,
        "LabourDay" => Strings.HolidayLabourDay,
        "LiberationDay" => Strings.HolidayLiberationDay,
        "CyrilMethodius" => Strings.HolidayCyrilMethodius,
        "JanHus" => Strings.HolidayJanHus,
        "StatehoodDay" => Strings.HolidayStatehoodDay,
        "IndependenceDay" => Strings.HolidayIndependenceDay,
        "FreedomDay" => Strings.HolidayFreedomDay,
        "ChristmasEve" => Strings.HolidayChristmasEve,
        "ChristmasDay" => Strings.HolidayChristmasDay,
        "StStephen" => Strings.HolidayStStephen,
        "GoodFriday" => Strings.HolidayGoodFriday,
        "EasterMonday" => Strings.HolidayEasterMonday,
        _ => ""
    };

    public override void Dispose()
    {
        self?.Dispose();
        base.Dispose();
    }
}
