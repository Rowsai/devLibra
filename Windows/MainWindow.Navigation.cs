using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Game.ClientState.Conditions;

namespace devLibra.Windows;

public sealed partial class MainWindow
{
    private int selectedPage;
    private static readonly (string Title, string Description, FontAwesomeIcon Icon)[] Pages =
    [
        ("ホーム", "機能の設定状況と現在の動作状態を確認できます。", FontAwesomeIcon.Home),
        ("Barrier HP", "パーティリストのHPにバリア量を加えて表示します。", FontAwesomeIcon.ShieldAlt),
        ("PartySearch", "近くにいるプレイヤーの表示とターゲット線を設定します。", FontAwesomeIcon.Users),
        ("Change Party Icons", "ターゲットマーカーの表示と並び替えを設定します。", FontAwesomeIcon.Bullseye),
        ("View DoT Icons", "DoTとデスデザインのアイコン・残り時間を設定します。", FontAwesomeIcon.Clock),
        ("Which Activate to PVP", "PvPコンテンツ参加中に使用する機能を選択します。", FontAwesomeIcon.SlidersH),
        ("プレイヤー情報", "周囲のプレイヤーと付与されているステータスを確認します。", FontAwesomeIcon.User),
        ("敵の詠唱", "敵が詠唱しているアクションを確認します。", FontAwesomeIcon.Fire),
        ("敵の状態", "敵に付与されているステータスを確認します。", FontAwesomeIcon.List),
        ("ステータス検索", "ステータスを名前・IDから検索します。", FontAwesomeIcon.Search),
        ("アクション検索", "アクションを名前・IDから検索します。", FontAwesomeIcon.Bolt),
    ];

    public override void Draw()
    {
        var unit = ImGui.GetTextLineHeight() / 17f;
        var available = ImGui.GetContentRegionAvail();
        var sidebarWidth = Math.Min(270 * unit, Math.Max(190 * unit, available.X * 0.26f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.035f, 0.065f, 0.12f, 1));
        if (ImGui.BeginChild("Navigation", new Vector2(sidebarWidth, 0), false))
        {
            ImGui.TextColored(new Vector4(0.4f, 0.78f, 1, 1), "devLibra");
            ImGui.TextDisabled($"v{typeof(Plugin).Assembly.GetName().Version}");
            ImGui.Spacing();
            NavigationItem(0, unit);
            SectionLabel("便利機能");
            for (var i = 1; i <= 4; i++) NavigationItem(i, unit);
            SectionLabel("動作設定");
            NavigationItem(5, unit);
            SectionLabel("開発ツール");
            for (var i = 6; i < Pages.Length; i++) NavigationItem(i, unit);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.048f, 0.085f, 0.145f, 1));
        if (ImGui.BeginChild($"PageContent##{selectedPage}", Vector2.Zero, true))
        {
            var page = Pages[selectedPage];
            Icon(page.Icon);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.9f, 0.96f, 1f, 1), page.Title);
            ImGui.TextWrapped(page.Description);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.PushItemWidth(Math.Clamp(ImGui.GetContentRegionAvail().X * 0.48f, 150 * unit, 380 * unit));
            switch (selectedPage)
            {
                case 0: DrawHomePage(); break;
                case 1: DrawBarrierHpTab(); break;
                case 2: DrawPartySearchTab(); break;
                case 3: DrawChangePartyIconsPage(); break;
                case 4: DrawDotIconsPage(); break;
                case 5: DrawPvpPage(); break;
                case 6: DrawPartyMemberTab(); break;
                case 7: DrawEnemyCastingTab(); break;
                case 8: DrawEnemyStatusTab(); break;
                case 9: DrawStatusSearchTab(); break;
                case 10: DrawActionSearchTab(); break;
            }
            ImGui.PopItemWidth();
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private static void SectionLabel(string title)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(title);
    }

    private static void Icon(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(icon.ToIconString());
        ImGui.PopFont();
    }

    private void NavigationItem(int page, float unit)
    {
        ImGui.PushID(page);
        var position = ImGui.GetCursorScreenPos();
        var height = 41 * unit;
        var selected = selectedPage == page;
        if (ImGui.Selectable("##NavigationItem", selected, ImGuiSelectableFlags.None, new Vector2(0, height)))
            selectedPage = page;
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(selected ? new Vector4(0.55f, 0.83f, 1, 1) : new Vector4(0.65f, 0.74f, 0.86f, 1));
        if (selected)
            draw.AddRectFilled(position + new Vector2(0, 5 * unit), position + new Vector2(3 * unit, height - 5 * unit), color, 2);
        ImGui.PushFont(UiBuilder.IconFont);
        draw.AddText(position + new Vector2(14 * unit, (height - ImGui.GetTextLineHeight()) / 2), color, Pages[page].Icon.ToIconString());
        ImGui.PopFont();
        var textPosition = position + new Vector2(43 * unit, (height - ImGui.GetTextLineHeight()) / 2);
        // Keep long menu names inside the sidebar even at small window sizes.
        var textWidth = ImGui.CalcTextSize(Pages[page].Title).X;
        var room = Math.Max(1, ImGui.GetContentRegionAvail().X - 48 * unit);
        var fontSize = ImGui.GetFontSize() * Math.Min(1, room / Math.Max(1, textWidth));
        draw.AddText(ImGui.GetFont(), fontSize, textPosition, color, Pages[page].Title);
        ImGui.PopID();
    }

    private void DrawHomePage()
    {
        var c = Plugin.Configuration;
        var enabled = (c.ShowBarrierAdjustedHp ? 1 : 0) + (c.PartySearchEnabled ? 1 : 0)
            + (c.ChangePartyIconsEnabled ? 1 : 0) + (c.ViewDotIconsEnabled ? 1 : 0);
        var allowed = (c.PvpAllowBarrierHp ? 1 : 0) + (c.PvpAllowPartySearch ? 1 : 0)
            + (c.PvpAllowChangePartyIcons ? 1 : 0) + (c.PvpAllowViewDotIcons ? 1 : 0);
        if (ImGui.BeginTable("HomeSummary", 3, ImGuiTableFlags.SizingStretchSame))
        {
            Summary("有効な便利機能", $"{enabled} / 4", "各機能の通常設定");
            Summary("PvP中の使用許可", $"{allowed} / 4", "通常設定が有効な機能に適用");
            Summary("現在の環境", Plugin.ObjectTable.LocalPlayer == null ? "未ログイン" :
                Plugin.InPvpContent ? "PvP" : Plugin.Condition[ConditionFlag.InCombat] ? "戦闘中" : "通常", "状態は自動更新されます");
            ImGui.EndTable();
        }
        ImGui.Spacing();
        DrawGeneralTab();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("表示設定の概要");
        var positions = new[] { "上", "右", "左" };
        ImGui.BulletText($"DoTアイコン：{c.DotIconSize}px / 数値：{c.DotTimerFontSize}px / 表示位置：{positions[Math.Clamp(c.DotIconPosition, 0, 2)]}");
        ImGui.BulletText($"数値の縁取り：{(c.DotTimerOutlineEnabled ? "有効" : "無効")} / オリジナル順：{(c.OriginalTargetMarkerSortDescending ? "降順" : "昇順")}");
        ImGui.Spacing();
        if (ImGui.Button("DoT表示の設定へ")) selectedPage = 4;
        ImGui.SameLine();
        if (ImGui.Button("PvPの動作設定へ")) selectedPage = 5;
    }

    private static void Summary(string title, string value, string description)
    {
        ImGui.TableNextColumn();
        if (ImGui.BeginChild(title, new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 4.8f), true))
        {
            ImGui.TextDisabled(title);
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), value);
            ImGui.TextWrapped(description);
        }
        ImGui.EndChild();
    }
}

