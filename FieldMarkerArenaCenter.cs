using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
namespace devLibra;
internal static class FieldMarkerArenaCenter
{
    internal sealed class Entry
    {
        public uint ContentId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public string Source { get; set; } = "";
    }
    private static readonly Entry[] Entries = Load();
    private static Entry[] Load()
    {
        using var stream = typeof(FieldMarkerArenaCenter).Assembly.GetManifestResourceStream("devLibra.ArenaCenters.json");
        return stream == null ? [] : JsonSerializer.Deserialize<Entry[]>(stream) ?? [];
    }
    internal static Entry[] Current()
    {
        var territory = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(Plugin.ClientState.TerritoryType);
        var id = territory?.ContentFinderCondition.RowId ?? 0;
        return Entries.Where(e => e.ContentId == id).ToArray();
    }
    internal static bool TryFloor(float x, float z, out Vector3 position)
    {
        position = default;
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null || !float.IsFinite(x) || !float.IsFinite(z)) return false;
        // Player height chooses the loaded floor only; horizontal center is always
        // the catalog coordinate, never the player's X/Z or a marker average.
        var origin = new Vector3(x, player.Position.Y + 5, z);
        if (!BGCollisionModule.RaycastMaterialFilter(origin, -Vector3.UnitY, out var hit, 15)) return false;
        position = hit.Point;
        return float.IsFinite(position.Y) && Math.Abs(position.X - x) < 0.1f && Math.Abs(position.Z - z) < 0.1f;
    }
    internal static int Nearest(Entry[] entries)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null || entries.Length == 0) return -1;
        var point = new Vector2(player.Position.X, player.Position.Z);
        return Enumerable.Range(0, entries.Length).OrderBy(i => Vector2.DistanceSquared(point, new(entries[i].X, entries[i].Z))).First();
    }
}
