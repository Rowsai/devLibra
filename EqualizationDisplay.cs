using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace devLibra;

internal sealed unsafe class EqualizationDisplay
{
    internal bool EditPosition;
    private bool dragging;
    internal static uint CurrentContent => Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()
        .GetRowOrDefault(Plugin.ClientState.TerritoryType)?.ContentFinderCondition.RowId ?? 0;

    internal void Draw()
    {
        var c = Plugin.Configuration;
        if (!EditPosition && (!c.EqualizationEnabled || Plugin.ObjectTable.LocalPlayer == null ||
            CurrentContent == 0 || !c.EqualizationContents.Contains(CurrentContent))) return;
        var lines = new List<string>();
        var percentages = new List<double>();
        if (EditPosition)
        {
            lines.Add("エネミーA  20.0%"); lines.Add("エネミーB  15.0%");
            percentages.Add(20); percentages.Add(15);
        }
        else
        {
            var list = EnemyListNumberArray.Instance();
            if (list == null || list->EnemyCount < 2) return;
            var ids = new HashSet<uint>();
            for (var i = 0; i < Math.Clamp(list->EnemyCount, 0, 8); i++)
            {
                var entry = list->Enemies[i];
                if (!entry.ActiveInList || !ids.Add((uint)entry.EntityId)) continue;
                var enemy = Plugin.ObjectTable.FirstOrDefault(o => o.EntityId == (uint)entry.EntityId) as IBattleChara;
                // Use actual HP, not the enemy list's rounded integer percentage.
                if (enemy == null || enemy.MaxHp == 0) continue;
                var percent = 100d * enemy.CurrentHp / enemy.MaxHp;
                percentages.Add(percent);
                lines.Add($"{enemy.Name.TextValue}  {percent:F1}%");
            }
        }
        if (percentages.Count < 2) return;
        var viewport = ImGui.GetMainViewport();
        var position = new Vector2(c.EqualizationX, c.EqualizationY);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) position = new(400, 300);
        position = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport.Size - new Vector2(50)));
        ImGui.SetNextWindowPos(viewport.Pos + position, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.8f);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;
        if (!EditPosition) flags |= ImGuiWindowFlags.NoInputs;
        if (ImGui.Begin("##devLibraEqualization", flags))
        {
            ImGui.TextColored(new Vector4(0.5f, 0.85f, 1, 1), $"HP差分：{percentages.Max() - percentages.Min():F1}%");
            foreach (var line in lines) ImGui.TextUnformatted(line);
            if (EditPosition && ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) dragging = true;
            if (dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                position = Vector2.Clamp(position + ImGui.GetIO().MouseDelta, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport.Size - ImGui.GetWindowSize()));
                c.EqualizationX = position.X; c.EqualizationY = position.Y;
            }
            if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { dragging = false; Plugin.SaveConfiguration(); }
        }
        ImGui.End();
    }
}
