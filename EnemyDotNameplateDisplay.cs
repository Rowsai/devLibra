using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using StatusRow = Lumina.Excel.Sheets.Status;

namespace devLibra;

/// <summary>Read-only overlay anchored to currently visible native nameplates.</summary>
internal sealed unsafe class EnemyDotNameplateDisplay : IDisposable
{
    private readonly Dictionary<uint, uint> dotIcons = new();
    private readonly List<(uint StatusId, uint Icon, float Remaining)> visibleDots = new();
    private readonly bool commandRegistered;
    private long nextErrorLogAt;
    internal bool CommandRegistered => commandRegistered;
    internal string? CatalogError { get; private set; }

    public EnemyDotNameplateDisplay()
    {
        try
        {
            foreach (var row in Plugin.DataManager.GetExcelSheet<StatusRow>(ClientLanguage.English))
                if (DotDisplayRules.IsDot(row.StatusCategory, row.Icon, row.Description.ExtractText()))
                    dotIcons[row.RowId] = row.Icon;
            if (!dotIcons.ContainsKey(1895) || !dotIcons.ContainsKey(1871))
                throw new InvalidOperationException("The DoT catalog is missing Biolysis or Dia.");
        }
        catch (Exception ex)
        {
            dotIcons.Clear();
            CatalogError = "DoTのゲームデータを読み込めませんでした。プラグインを再読み込みしてください。";
            Plugin.Log.Error(ex, "Failed to load the DoT status catalog.");
        }
        commandRegistered = Plugin.CommandManager.AddHandler("/dl", new CommandInfo(OnCommand)
        {
            HelpMessage = "/dl show dot: 自分が付与したDoTのネームプレート表示をON/OFFにします。",
        });
        if (!commandRegistered)
            Plugin.ChatGui.PrintError("[devLibra]/dl は他のプラグインに登録されているため使用できません。");
    }

    private void OnCommand(string command, string arguments)
    {
        if (!DotDisplayRules.IsToggleCommand(arguments))
        {
            Plugin.ChatGui.PrintError("[devLibra]使い方: /dl show dot");
            return;
        }
        Plugin.Configuration.ViewDotIconsEnabled = !Plugin.Configuration.ViewDotIconsEnabled;
        Plugin.SaveConfiguration();
        Plugin.ChatGui.Print($"[devLibra]View DoT Icons: {(Plugin.Configuration.ViewDotIconsEnabled ? "ON" : "OFF")}");
    }

    public void Draw()
    {
        if (!Plugin.Configuration.ViewDotIconsEnabled || CatalogError != null) return;
        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return;
        try { DrawNameplates(local.EntityId); }
        catch (Exception ex)
        {
            // A transient addon rebuild should not spam one exception per frame.
            if (Environment.TickCount64 >= nextErrorLogAt)
            {
                nextErrorLogAt = Environment.TickCount64 + 5000;
                Plugin.Log.Warning(ex, "Failed to draw enemy DoT nameplates.");
            }
        }
    }

    private void DrawNameplates(uint localId)
    {
        var addon = Plugin.GameGui.GetAddonByName<AddonNamePlate>("NamePlate");
        if (addon == null || !addon->IsVisible || addon->NamePlateObjectArray == null) return;
        var numbers = NamePlateNumberArray.Instance();
        if (numbers == null) return;
        var viewport = ImGui.GetMainViewport();
        var draw = ImGui.GetBackgroundDrawList();
        for (var i = 0; i < Math.Clamp(numbers->ActiveNamePlateCount, 0, 50); i++)
        {
            var data = numbers->ObjectData[i];
            if (data.EntityId is 0 or 0xE0000000 || (data.VisibilityFlags & 3) == 0 ||
                data.NamePlateObjectIndex < 0 || data.NamePlateObjectIndex >= 50) continue;
            if (Plugin.ObjectTable.SearchByEntityId(data.EntityId) is not IBattleChara enemy ||
                enemy.ObjectKind != ObjectKind.BattleNpc || enemy.CurrentHp == 0) continue;
            var plate = addon->NamePlateObjectArray[data.NamePlateObjectIndex];
            var root = plate.RootComponentNode;
            if (root == null || !IsVisible((AtkResNode*)root)) continue;

            visibleDots.Clear();
            foreach (var status in enemy.StatusList)
            {
                if (DotDisplayRules.ShouldDisplay(localId, status.SourceId, status.RemainingTime) &&
                    dotIcons.TryGetValue(status.StatusId, out var icon))
                    visibleDots.Add((status.StatusId, icon, status.RemainingTime));
            }
            if (visibleDots.Count == 0) continue;
            // Status-list slot reuse must not shuffle icons when a DoT is refreshed.
            visibleDots.Sort((left, right) => left.StatusId.CompareTo(right.StatusId));
            var anchor = plate.NameplateCollision != null
                ? (AtkResNode*)plate.NameplateCollision : (AtkResNode*)root;
            var scale = GetScale(anchor);
            var size = (float)Math.Clamp(Plugin.Configuration.DotIconSize, 16, 64);
            var gap = Math.Max(2f, 3f * scale);
            var width = visibleDots.Count * size + (visibleDots.Count - 1) * gap;
            var plateWidth = anchor->Width * scale;
            var plateHeight = anchor->Height * GetScale(anchor, vertical: true);
            var origin = viewport.Pos + new Vector2(anchor->ScreenX, anchor->ScreenY) +
                Math.Clamp(Plugin.Configuration.DotIconPosition, 0, 2) switch
                {
                    1 => new Vector2(plateWidth + gap, (plateHeight - size) / 2),
                    2 => new Vector2(-width - gap, (plateHeight - size) / 2),
                    _ => new Vector2((plateWidth - width) / 2, -size - gap),
                };
            if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) ||
                origin.X + width < viewport.Pos.X || origin.X > viewport.Pos.X + viewport.Size.X ||
                origin.Y + size < viewport.Pos.Y || origin.Y > viewport.Pos.Y + viewport.Size.Y) continue;
            for (var index = 0; index < visibleDots.Count; index++)
            {
                var dot = visibleDots[index];
                var texture = Plugin.TextureProvider.GetFromGameIcon(dot.Icon).GetWrapOrDefault();
                if (texture == null) continue;
                var topLeft = origin + new Vector2(index * (size + gap), 0);
                var bottomRight = topLeft + new Vector2(size, size);
                draw.AddImage(texture.Handle, topLeft, bottomRight);
                var text = DotDisplayRules.TimeText(dot.Remaining);
                var textSize = ImGui.CalcTextSize(text);
                var requestedScale = Math.Clamp(Plugin.Configuration.DotTimerFontSize, 8, 48)
                    / Math.Max(1f, ImGui.GetFontSize());
                var textScale = Math.Min(requestedScale, Math.Min((size - 4) / Math.Max(1, textSize.X),
                    (size - 4) / Math.Max(1, textSize.Y)));
                textSize *= textScale;
                var fontSize = ImGui.GetFontSize() * textScale;
                var textPosition = topLeft + (new Vector2(size) - textSize) / 2;
                draw.AddRectFilled(textPosition - new Vector2(2, 0),
                    textPosition + textSize + new Vector2(2, 0), 0xB0000000);
                draw.AddText(ImGui.GetFont(), fontSize, textPosition + Vector2.One, 0xFF000000, text);
                draw.AddText(ImGui.GetFont(), fontSize, textPosition,
                    dot.Remaining <= 5 ? 0xFF50C0FF : 0xFFFFFFFF, text);
            }
        }
    }

    private static bool IsVisible(AtkResNode* node)
    {
        for (var depth = 0; node != null && depth < 32; depth++, node = node->ParentNode)
            if (!node->IsVisible() || node->Color.A == 0) return false;
        return node == null;
    }

    private static float GetScale(AtkResNode* node, bool vertical = false)
    {
        var scale = 1f;
        for (var depth = 0; node != null && depth < 32; depth++, node = node->ParentNode)
            scale *= vertical ? node->ScaleY : node->ScaleX;
        return float.IsFinite(scale) && scale > 0 ? scale : 1f;
    }

    public void Dispose()
    {
        if (commandRegistered) Plugin.CommandManager.RemoveHandler("/dl");
        dotIcons.Clear();
        visibleDots.Clear();
    }
}
