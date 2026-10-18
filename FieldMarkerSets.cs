using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace devLibra;

public sealed class SavedFieldMarker
{
    public bool Active { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    internal Vector3 Position => new(X, Y, Z);
    internal bool Valid => !Active || (float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) &&
        Math.Abs((double)X * 1000) < int.MaxValue && Math.Abs((double)Y * 1000) < int.MaxValue && Math.Abs((double)Z * 1000) < int.MaxValue);
}

public sealed class SavedFieldMarkerSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ContentKey { get; set; } = string.Empty;
    public string ContentName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SavedFieldMarker[] Markers { get; set; } = [];
    internal bool Valid => Markers is { Length: 8 } && Markers.All(m => m != null && m.Valid);
}

internal static class FieldMarkerSets
{
    internal static bool CanSave(IEnumerable<SavedFieldMarkerSet> sets, string key) => sets.Count(s => s.ContentKey == key) < 3;
    internal static string DefaultName(IEnumerable<SavedFieldMarkerSet> sets, string key, string contentName)
    {
        var names = sets.Where(s => s.ContentKey == key).Select(s => s.Name).ToHashSet();
        for (var n = 1; ; n++)
            if (!names.Contains($"{contentName}{n}")) return $"{contentName}{n}";
    }
}
