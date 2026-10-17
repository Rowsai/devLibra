using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace devLibra;

/// <summary>Search overlay attached to the native title window. Never rewrites the native list.</summary>
internal sealed unsafe class TitlesSearchDisplay
{
    private string query = string.Empty;
    private readonly List<(Title Row, string Male, string Female)> catalog = [];
    private nint lastAddon;
    private ulong owner;
    private long retryAt;
    private Title? pendingTitle;
    private string pendingLabel = string.Empty;
    private const string ConfirmationPopup = "称号の設定確認##devLibraTitleConfirm";

    internal void Draw()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("CharacterTitle").Address;
        if (!Plugin.Configuration.TitlesSearchEnabled || player == null || addon == null || !addon->IsVisible || addon->RootNode == null)
        {
            query = string.Empty;
            lastAddon = 0;
            pendingTitle = null;
            return;
        }
        if (lastAddon != (nint)addon || owner != player.GameObjectId)
        {
            query = string.Empty;
            pendingTitle = null;
            lastAddon = (nint)addon;
            owner = player.GameObjectId;
        }
        if (Environment.TickCount64 < retryAt) return;
        try { DrawSearch(addon); }
        catch (Exception ex)
        {
            retryAt = Environment.TickCount64 + 5000;
            Plugin.Log.Error(ex, "Titles Search overlay failed.");
        }
    }

    private void DrawSearch(AtkUnitBase* addon)
    {
        if (catalog.Count == 0)
            foreach (var row in Plugin.DataManager.GetExcelSheet<Title>())
                if (row.RowId > 0 && row.RowId <= ushort.MaxValue)
                    catalog.Add((row, row.Masculine.ExtractText(), row.Feminine.ExtractText()));

        var scale = addon->Scale;
        var width = Math.Max(240, addon->RootNode->Width * scale);
        var barHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2;
        var viewport = ImGui.GetMainViewport();
        var position = viewport.Pos + new Vector2(addon->X, addon->Y - barHeight);
        position.Y = Math.Max(viewport.Pos.Y, position.Y);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNavFocus;
        ImGui.SetNextWindowPos(position);
        ImGui.SetNextWindowSize(new Vector2(width, barHeight));
        var show = ImGui.Begin("称号を検索##devLibraTitleSearch", flags);
        try
        {
            if (show)
            {
                ImGui.SetNextItemWidth(Math.Max(100, ImGui.GetContentRegionAvail().X - 65));
                ImGui.InputTextWithHint("##Query", "称号を部分一致で検索", ref query, 256);
                ImGui.SameLine();
                if (ImGui.Button("クリア")) query = string.Empty;
            }
        }
        finally { ImGui.End(); }

        if (string.IsNullOrWhiteSpace(query)) return;
        // Keep the native window's title bar visible so it can still be moved and closed.
        var resultTop = viewport.Pos + new Vector2(addon->X + 8 * scale, addon->Y + 42 * scale);
        ImGui.SetNextWindowPos(resultTop);
        ImGui.SetNextWindowSize(new Vector2(width - 16 * scale, Math.Max(100, (addon->RootNode->Height - 54) * scale)));
        ImGui.SetNextWindowBgAlpha(1);
        show = ImGui.Begin("検索結果##devLibraTitleResults", flags);
        try
        {
            if (!show) return;
            if (!Plugin.UnlockState.IsTitleListLoaded)
            {
                ImGui.TextUnformatted("取得済みの称号を読み込み中です…");
                return;
            }
            var count = 0;
            foreach (var entry in catalog)
            {
                if (!Plugin.UnlockState.IsTitleUnlocked(entry.Row) ||
                    !(TitleSearchRules.Matches(entry.Male, query) || TitleSearchRules.Matches(entry.Female, query))) continue;
                count++;
                var label = entry.Male == entry.Female ? entry.Male : $"{entry.Male} / {entry.Female}";
                ImGui.PushID((int)entry.Row.RowId);
                var chosen = ImGui.Selectable(label);
                ImGui.PopID();
                if (chosen)
                {
                    pendingTitle = entry.Row;
                    pendingLabel = label;
                    ImGui.OpenPopup(ConfirmationPopup);
                    break;
                }
            }
            if (count == 0) ImGui.TextWrapped("一致する取得済みの称号はありません。");
            DrawConfirmation();
        }
        finally { ImGui.End(); }
    }

    private void DrawConfirmation()
    {
        if (!ImGui.BeginPopupModal(ConfirmationPopup, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)) return;
        try
        {
            if (pendingTitle is not { } title)
            {
                ImGui.CloseCurrentPopup();
                return;
            }
            ImGui.TextUnformatted($"称号を設定しますか？対象：{pendingLabel}");
            ImGui.Spacing();
            if (ImGui.Button("OK", new Vector2(100, 0)))
            {
                // Recheck ownership before sending the game's title change request.
                if (Plugin.UnlockState.IsTitleListLoaded && Plugin.UnlockState.IsTitleUnlocked(title))
                {
                    UIState.Instance()->TitleController.SendTitleIdUpdate((ushort)title.RowId);
                    query = string.Empty;
                }
                pendingTitle = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100, 0)))
            {
                pendingTitle = null;
                ImGui.CloseCurrentPopup();
            }
        }
        finally { ImGui.EndPopup(); }
    }
}
