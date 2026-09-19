using System;
using System.Collections.Generic;
using System.Linq;

namespace devLibra;

internal enum PartySortMode { None, Ascending, Descending, Original, Default }

/// <summary>Pure sorting logic. Marker values are MarkingController indices.</summary>
internal static class TargetMarkerSortOrder
{
    internal static int[] DefaultOrder() => [0, 1, 2, 3, 4, 14, 15, 16, 5, 6, 7, 8, 9];

    internal static int[] Normalize(IEnumerable<int>? order)
    {
        var valid = DefaultOrder();
        return (order ?? []).Where(valid.Contains).Concat(valid).Distinct().ToArray();
    }

    internal static string Label(int marker) => marker switch
    {
        >= 0 and <= 4 => $"攻撃{marker + 1}",
        >= 14 and <= 16 => $"攻撃{marker - 8}",
        >= 5 and <= 7 => $"足止め{marker - 4}",
        8 or 9 => $"禁止{marker - 7}",
        _ => "対象外",
    };

    internal static PartySortMode Parse(string arguments)
    {
        var words = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1 && words[0].Equals("default", StringComparison.OrdinalIgnoreCase))
            return PartySortMode.Default;
        if (words.Length != 2 || !words[0].Equals("tm", StringComparison.OrdinalIgnoreCase))
            return PartySortMode.None;
        return words[1].ToLowerInvariant() switch
        {
            "asc" => PartySortMode.Ascending,
            "desc" => PartySortMode.Descending,
            "original" => PartySortMode.Original,
            _ => PartySortMode.None,
        };
    }

    // Result maps destination row -> original row. Row zero (self), unmarked
    // members and shape markers remain in their original final positions.
    internal static int[] BuildTarget(int[] markers, int[] order, bool descending)
    {
        var ranks = Normalize(order);
        if (descending) Array.Reverse(ranks);
        var eligible = Enumerable.Range(1, Math.Max(0, markers.Length - 1))
            .Where(row => Array.IndexOf(ranks, markers[row]) >= 0).ToArray();
        var sorted = eligible.OrderBy(row => Array.IndexOf(ranks, markers[row])).ToArray();
        var target = Enumerable.Range(0, markers.Length).ToArray();
        for (var i = 0; i < eligible.Length; i++) target[eligible[i]] = sorted[i];
        return target;
    }

    // Adjacent exchanges avoid depending on whether native non-adjacent
    // ChangeOrder behaves as an insertion or an exchange.
    internal static List<(int First, int Second)> BuildSwaps(int[] target)
    {
        if (!target.Order().SequenceEqual(Enumerable.Range(0, target.Length)) ||
            (target.Length > 0 && target[0] != 0))
            throw new ArgumentException("Target must be a permutation with self fixed.", nameof(target));
        var working = Enumerable.Range(0, target.Length).ToArray();
        var swaps = new List<(int, int)>();
        for (var row = 1; row < target.Length; row++)
        {
            var source = Array.IndexOf(working, target[row], row);
            for (; source > row; source--)
            {
                swaps.Add((source - 1, source));
                (working[source - 1], working[source]) = (working[source], working[source - 1]);
            }
        }
        return swaps;
    }
}
