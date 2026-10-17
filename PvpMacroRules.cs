using System;
using System.Collections.Generic;

namespace devLibra;

internal static class PvpMacroRules
{
    internal static bool IsValid(int id) => id >= 0 && (id >> 8) <= 1 && (id & 255) < 100;

    internal static int[] Normalize(int[]? source)
    {
        var result = new[] { -1, -1, -1, -1, -1 };
        var seen = new HashSet<int>();
        for (var i = 0; i < Math.Min(source?.Length ?? 0, 5); i++)
            if (IsValid(source![i]) && seen.Add(source[i])) result[i] = source[i];
        return result;
    }

    internal static int[] Assign(int[]? source, int row, int macro)
    {
        var result = Normalize(source);
        if (row < 0 || row >= 5) return result;
        if (!IsValid(macro)) macro = -1;
        for (var i = 0; i < 5; i++)
            if (result[i] == macro) result[i] = -1;
        result[row] = macro;
        return result;
    }
}
