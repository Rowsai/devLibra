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
        ("Titles Search", "ゲームの称号一覧で、取得済みの称号を部分一致検索します。", FontAwesomeIcon.Search),
        ("For PvP Battle", "クリスタルコンフリクトの敵リストとマクロアイコンを連動させます。", FontAwesomeIcon.Bullseye),
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
            NavigationItem(11, unit);
            NavigationItem(12, unit);
            SectionLabel("動作設定");
            NavigationItem(5, unit);
            SectionLabel("開発ツール");
            for (var i = 6; i <= 10; i++) NavigationItem(i, unit);
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
                case 11: DrawTitlesSearchPage(); break;
                case 12: DrawPvpBattlePage(); break;
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
            + (c.ChangePartyIconsEnabled ? 1 : 0) + (c.ViewDotIconsEnabled ? 1 : 0) + (c.TitlesSearchEnabled ? 1 : 0)
            + (c.ForPvpBattleEnabled ? 1 : 0);
        var allowed = (c.PvpAllowBarrierHp ? 1 : 0) + (c.PvpAllowPartySearch ? 1 : 0)
            + (c.PvpAllowChangePartyIcons ? 1 : 0) + (c.PvpAllowViewDotIcons ? 1 : 0);
        if (ImGui.BeginTable("HomeSummary", 3, ImGuiTableFlags.SizingStretchSame))
        {
            Summary("有効な便利機能", $"{enabled} / 6", "各機能の通常設定");
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

    private static void DrawTitlesSearchPage()
    {
        var enabled = Plugin.Configuration.TitlesSearchEnabled;
        ImGui.TextUnformatted($"現在の設定：{(enabled ? "有効" : "無効")}");
        if (ImGui.Button(enabled ? "Titles Search を無効にする" : "Titles Search を有効にする"))
        {
            Plugin.Configuration.TitlesSearchEnabled = !enabled;
            Plugin.SaveConfiguration();
        }
        ImGui.Spacing();
        ImGui.TextWrapped("有効にしてゲームの称号一覧を開くと、上部に検索バーが表示されます。文字を入力すると、取得済みの称号から部分一致で検索します。");
        ImGui.TextWrapped("検索結果をクリックすると確認画面を表示します。OKで称号を変更し、Cancelで変更せず検索結果へ戻ります。「クリア」で元の一覧へ戻ります。英字の大文字・小文字、全角・半角、ひらがな・カタカナを区別しません。");
    }

    private static void DrawPvpBattlePage()
    {
        var enabled = Plugin.Configuration.ForPvpBattleEnabled;
        ImGui.TextUnformatted($"現在の設定：{(enabled ? "有効" : "無効")}");
        if (ImGui.Button(enabled ? "For PvP Battle を無効にする" : "For PvP Battle を有効にする"))
        {
            Plugin.Configuration.ForPvpBattleEnabled = !enabled;
            Plugin.SaveConfiguration();
        }
        ImGui.Spacing();
        ImGui.TextWrapped(Plugin.PvpBattleError ?? (!enabled ? "機能は無効です。" :
            PvpBattleMacroIcons.InCrystallineConflict ? "クリスタルコンフリクト内で自動更新中です。" : "クリスタルコンフリクト参加待ちです。"));
        ImGui.TextWrapped("敵リスト1～5番ごとに、ジョブアイコンへ変更するマクロを指定してください。マクロの実行やマーカー付与は不要です。設定は自動保存します。");
        ImGui.TextWrapped("通常・クロスホットバーに置かれた指定マクロを自動更新します。同じマクロを複数のスロットに置いた場合はすべて反映します。マクロ本文と実行内容は変更しません。");
        ImGui.Spacing();
        for (var row = 0; row < 5; row++)
        {
            ImGui.PushID(row);
            ImGui.Separator();
            ImGui.TextUnformatted($"敵リスト {row + 1}（<e{row + 1}>）");
            ImGui.TextDisabled(Plugin.PvpEnemyLabel(row));
            var selected = Plugin.Configuration.PvpEnemyMacros[row];
            ImGui.SetNextItemWidth(Math.Max(200, ImGui.GetContentRegionAvail().X - 16));
            if (ImGui.BeginCombo("##Macro", PvpBattleMacroIcons.MacroLabel(selected)))
            {
                if (ImGui.Selectable("未設定", selected == -1)) Assign(-1);
                for (var set = 0; set < 2; set++)
                    for (var index = 0; index < 100; index++)
                    {
                        var id = (set << 8) | index;
                        if (ImGui.Selectable($"{PvpBattleMacroIcons.MacroLabel(id)}##{id}", selected == id)) Assign(id);
                    }
                ImGui.EndCombo();
            }
            ImGui.PopID();
            void Assign(int id)
            {
                Plugin.Configuration.PvpEnemyMacros = PvpMacroRules.Assign(Plugin.Configuration.PvpEnemyMacros, row, id);
                Plugin.SaveConfiguration();
            }
        }
        ImGui.Spacing();
        ImGui.TextWrapped("同じマクロを別の敵番号に選ぶと、以前の割り当てを解除します。対象を取得できない場合、機能無効時、退出時は元のアイコンを表示します。");
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

