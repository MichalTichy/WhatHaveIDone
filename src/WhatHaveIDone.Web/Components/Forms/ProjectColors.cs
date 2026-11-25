using System.Globalization;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Components.Forms;

public static class ProjectColors
{
    public static readonly string[] Palette = ["#009cde", "#2563eb", "#4f46e5", "#7c3aed", "#a21caf", "#db2777", "#dc2626", "#ea580c", "#b45309", "#ca8a04", "#65a30d", "#16a34a", "#059669", "#0d9488", "#0891b2", "#687483"];

    // Grey is the "no project" colour, so it is never suggested.
    private static readonly string[] Candidates = Palette[..^1];

    /// <summary>Palette colour most distinct from the ten projects with the most time in the last 30 days.</summary>
    public static string Suggest(UserContext user, string? excludedProjectId)
    {
        var used = user.Projects
            .Where(p => p.Id != excludedProjectId && user.RecentProjectSeconds(p.Id) > 0)
            .OrderByDescending(p => user.RecentProjectSeconds(p.Id))
            .Take(10)
            .Select(p => p.Color)
            .ToList();
        return Suggest(used);
    }

    public static string Suggest(IReadOnlyCollection<string> usedColors)
    {
        var used = usedColors.Select(ToOklab).OfType<(double L, double A, double B)>().ToList();
        if (used.Count == 0)
            return Candidates[0];
        return Candidates.MaxBy(candidate =>
        {
            var lab = ToOklab(candidate)!.Value;
            return used.Min(u => Math.Pow(lab.L - u.L, 2) + Math.Pow(lab.A - u.A, 2) + Math.Pow(lab.B - u.B, 2));
        })!;
    }

    // OKLab keeps Euclidean distance close to perceived colour difference.
    private static (double L, double A, double B)? ToOklab(string hex)
    {
        if (hex is not ['#', ..] || hex.Length != 7 || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return null;
        var r = Linear(rgb >> 16 & 0xff);
        var g = Linear(rgb >> 8 & 0xff);
        var b = Linear(rgb & 0xff);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static double Linear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
