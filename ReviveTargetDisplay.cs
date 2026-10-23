using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace devLibra;

internal sealed unsafe class ReviveTargetDisplay
{
    private readonly HashSet<uint> raiseActions = [];
    private long nextLog;
    internal string? Error { get; private set; }

    internal ReviveTargetDisplay()
    {
        try
        {
            foreach (var action in Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>())
                if (action.DeadTargetBehaviour == 1 && action.CanTargetParty && action.CastType == 1)
                    raiseActions.Add(action.RowId);
            foreach (var required in new uint[] { 125, 173, 3603, 7523, 18317, 24287 })
                if (!raiseActions.Contains(required)) throw new InvalidOperationException($"Missing resurrection action {required}");
        }
        catch (Exception ex)
        {
            raiseActions.Clear();
            Error = "蘇生アクションのデータを取得できませんでした。";
            Plugin.Log.Error(ex, "Failed to load revive actions.");
        }
    }

    internal void Draw()
    {
        if (!Plugin.Configuration.CheckReviveTargetEnabled || Plugin.ObjectTable.LocalPlayer == null || Error != null) return;
        try
        {
            var draw = ImGui.GetBackgroundDrawList();
            var viewport = ImGui.GetMainViewport();
            var iconSize = Math.Max(24, ImGui.GetTextLineHeight() * 1.5f);
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is not IPlayerCharacter caster || !caster.IsCasting || caster.CastActionType != 1 ||
                    !raiseActions.Contains(caster.CastActionId) || caster.CurrentHp == 0) continue;
                // CastTargetObjectId is the actual spell target, even when the caster switches selection.
                if (Plugin.ObjectTable.SearchById(caster.CastTargetObjectId) is not IPlayerCharacter target ||
                    target.GameObjectId == caster.GameObjectId || target.ClassJob.RowId == 0) continue;
                var name = target.Name.TextValue;
                if (string.IsNullOrWhiteSpace(name)) continue;
                var native = (NativeObject*)caster.Address;
                if (native == null) continue;
                var height = native->GetHeight();
                if (!float.IsFinite(height) || height <= 0) height = 2;
                if (!Plugin.GameGui.WorldToScreen(caster.Position + Vector3.UnitY * (height + 0.35f), out var anchor)) continue;
                var label = $"蘇生 → {name}";
                var textSize = ImGui.CalcTextSize(label);
                var width = iconSize + 6 + textSize.X;
                var start = viewport.Pos + anchor - new Vector2(width / 2, iconSize + 8);
                if (!float.IsFinite(start.X) || !float.IsFinite(start.Y) || start.X + width < viewport.Pos.X ||
                    start.X > viewport.Pos.X + viewport.Size.X || start.Y + iconSize < viewport.Pos.Y || start.Y > viewport.Pos.Y + viewport.Size.Y) continue;
                draw.AddRectFilled(start - new Vector2(5, 4), start + new Vector2(width + 5, iconSize + 4), 0xCC21130A, 6);
                var texture = Plugin.TextureProvider.GetFromGameIcon(62100 + target.ClassJob.RowId).GetWrapOrDefault();
                if (texture != null) draw.AddImage(texture.Handle, start, start + new Vector2(iconSize));
                var textPosition = start + new Vector2(iconSize + 6, (iconSize - textSize.Y) / 2);
                draw.AddText(textPosition + Vector2.One, 0xFF000000, label);
                draw.AddText(textPosition, 0xFFFFFFFF, label);
            }
        }
        catch (Exception ex)
        {
            if (Environment.TickCount64 < nextLog) return;
            nextLog = Environment.TickCount64 + 5000;
            Plugin.Log.Warning(ex, "Failed to draw revive target.");
        }
    }
}
