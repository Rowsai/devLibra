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

    internal static (int[] Enemy, int[] Ally) NormalizeTeams(int[]? enemy, int[]? ally)
    {
        var e = Normalize(enemy);
        var a = Normalize(ally);
        for (var i = 0; i < a.Length; i++)
            if (a[i] >= 0 && Array.IndexOf(e, a[i]) >= 0) a[i] = -1;
        return (e, a);
    }

    internal static (int[] Enemy, int[] Ally) AssignTeam(int[]? enemy, int[]? ally, bool toAlly, int row, int macro)
    {
        var (e, a) = NormalizeTeams(enemy, ally);
        if (row < 0 || row >= 5) return (e, a);
        var other = toAlly ? e : a;
        if (IsValid(macro))
            for (var i = 0; i < other.Length; i++) if (other[i] == macro) other[i] = -1;
        if (toAlly) a = Assign(a, row, macro); else e = Assign(e, row, macro);
        return (e, a);
    }
    internal static (int[] Enemy, int[] Ally) NormalizeSlots(int[]? enemy, int[]? ally, int[]? legacyEnemy = null, int[]? legacyAlly = null)
    {
        var seen = new HashSet<int>();
        int[] Convert(int[]? slots, int[]? legacy)
        {
            var result = new int[15];
            Array.Fill(result, -1);
            for (var i = 0; i < 15; i++)
            {
                var id = slots != null ? (i < slots.Length ? slots[i] : -1)
                    : (i % 3 == 0 && i / 3 < (legacy?.Length ?? 0) ? legacy![i / 3] : -1);
                if (IsValid(id) && seen.Add(id)) result[i] = id;
            }
            return result;
        }
        return (Convert(enemy, legacyEnemy), Convert(ally, legacyAlly));
    }

    internal static (int[] Enemy, int[] Ally) AssignSlot(int[]? enemy, int[]? ally, bool toAlly, int row, int slot, int macro)
    {
        var (e, a) = NormalizeSlots(enemy, ally);
        if (row < 0 || row >= 5 || slot < 0 || slot >= 3) return (e, a);
        if (!IsValid(macro)) macro = -1;
        if (macro >= 0)
            foreach (var team in new[] { e, a })
                for (var i = 0; i < team.Length; i++) if (team[i] == macro) team[i] = -1;
        (toAlly ? a : e)[row * 3 + slot] = macro;
        return (e, a);
    }}

