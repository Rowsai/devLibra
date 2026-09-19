using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace devLibra;

internal static class DotDisplayRules
{
    internal const uint DeathsDesignStatusId = 2586;

    internal static bool IsSupportedStatus(uint statusId, byte category, uint icon, string description)
        => category == 2 && icon != 0 &&
           (statusId == DeathsDesignStatusId || IsDot(category, icon, description));

    // Read the English sheet regardless of the user's client language. Require
    // a description of actual periodic damage, not merely a damage modifier.
    private static readonly Regex PeriodicDamage = new(
        @"\b(?:sustaining|suffering|taking|causing|inflicting|dealing)\b[^.!?]*\bdamage over time\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool IsDot(byte category, uint icon, string description)
        => category == 2 && icon != 0 && PeriodicDamage.IsMatch(description);

    internal static bool ShouldDisplay(uint localId, uint sourceId, float remaining)
        => localId is not (0 or 0xE0000000) && sourceId == localId &&
           float.IsFinite(remaining) && remaining > 0;

    internal static string TimeText(float remaining)
        => !float.IsFinite(remaining) || remaining <= 0 ? string.Empty :
            Math.Ceiling(remaining).ToString("0", CultureInfo.InvariantCulture);

    internal static bool IsToggleCommand(string arguments)
    {
        var words = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 2 && words[0].Equals("show", StringComparison.OrdinalIgnoreCase)
            && words[1].Equals("dot", StringComparison.OrdinalIgnoreCase);
    }
}
