using System;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Lumina.Text.ReadOnly;

namespace devLibra;

internal static class StatusRsvText
{
    internal static bool IsUnresolved(string text) => text.StartsWith("_rsv_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("rsv_", StringComparison.OrdinalIgnoreCase);

    internal static unsafe string Resolve(ReadOnlySeString source)
    {
        var text = source.ExtractText();
        if (!IsUnresolved(text)) return text;
        var world = LayoutWorld.Instance();
        if (world == null) return text;
        var resolved = world->ResolveRsvString(text);
        if (resolved.Value == null) return text;
        var result = resolved.AsReadOnlySeString().ExtractText();
        return string.IsNullOrEmpty(result) || IsUnresolved(result) ? text : result;
    }
}
