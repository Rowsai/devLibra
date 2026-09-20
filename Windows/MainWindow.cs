using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace devLibra.Windows;

public sealed class MainWindow : Window
{
    private string statusSearchText = string.Empty;
    private bool statusSearchExactMatch = false;

    private string actionSearchText = string.Empty;
    private bool actionSearchExactMatch = false;
    private ulong partyInviteTargetGameObjectId;

    public MainWindow()
        : base(
            $"devLibra  v{typeof(Plugin).Assembly.GetName().Version}###devLibra",
            ImGuiWindowFlags.None)
    {
        this.Size = new Vector2(1400, 800);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(0.35f, 0.75f, 1f, 1f), "devLibra / コントロールパネル");
        ImGui.TextDisabled("便利機能の状態確認とカスタマイズ");
        ImGui.Spacing();
        if (ImGui.BeginTabBar("devLibraTabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (ImGui.BeginTabItem("General"))
            {
                this.DrawGeneralTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("プレイヤー情報###PartyMember"))
            {
                this.DrawPartyMemberTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("敵の詠唱###EnemyCasting"))
            {
                this.DrawEnemyCastingTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("敵の状態###EnemyStatus"))
            {
                this.DrawEnemyStatusTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("ステータス検索###StatusSearch"))
            {
                this.DrawStatusSearchTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("アクション検索###ActionSearch"))
            {
                this.DrawActionSearchTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Barrier HP"))
            {
                this.DrawBarrierHpTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("PartySearch"))
            {
                this.DrawPartySearchTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Change Party Icons"))
            {
                if (!Plugin.PvpAllowsChangePartyIcons) ImGui.TextDisabled("PvP設定により現在停止中です。");
                var enabled = Plugin.Configuration.ChangePartyIconsEnabled;
                if (ImGui.Checkbox("有効##ChangePartyIcons", ref enabled))
                {
                    Plugin.Configuration.ChangePartyIconsEnabled = enabled;
                    Plugin.SaveConfiguration();
                }
                this.DrawTargetMarkerSortSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("View DoT Icons"))
            {
                if (!Plugin.PvpAllowsViewDotIcons) ImGui.TextDisabled("PvP設定により現在停止中です。");
                var dotEnabled = Plugin.Configuration.ViewDotIconsEnabled;
                if (ImGui.Checkbox("有効##ViewDotIcons", ref dotEnabled))
                {
                    Plugin.Configuration.ViewDotIconsEnabled = dotEnabled;
                    Plugin.SaveConfiguration();
                }
                var iconSize = Math.Clamp(Plugin.Configuration.DotIconSize, 16, 64);
                if (ImGui.SliderInt("アイコンサイズ", ref iconSize, 16, 64, "%d px"))
                {
                    Plugin.Configuration.DotIconSize = iconSize;
                    Plugin.SaveConfiguration();
                }
                var timerFontSize = Math.Clamp(Plugin.Configuration.DotTimerFontSize, 8, 48);
                if (ImGui.SliderInt("数値サイズ", ref timerFontSize, 8, 48, "%d px"))
                {
                    Plugin.Configuration.DotTimerFontSize = timerFontSize;
                    Plugin.SaveConfiguration();
                }
                ImGui.TextDisabled("数値がアイコン内に収まらない場合は、自動で縮小します。");
                var textColor = Plugin.Configuration.DotTimerTextColor;
                if (ImGui.ColorEdit4("文字色", ref textColor, ImGuiColorEditFlags.AlphaBar))
                {
                    Plugin.Configuration.DotTimerTextColor = textColor;
                    Plugin.SaveConfiguration();
                }
                var outlineEnabled = Plugin.Configuration.DotTimerOutlineEnabled;
                if (ImGui.Checkbox("文字を縁取りする", ref outlineEnabled))
                {
                    Plugin.Configuration.DotTimerOutlineEnabled = outlineEnabled;
                    Plugin.SaveConfiguration();
                }
                if (outlineEnabled)
                {
                    var outlineColor = Plugin.Configuration.DotTimerOutlineColor;
                    if (ImGui.ColorEdit4("縁取りの色", ref outlineColor, ImGuiColorEditFlags.AlphaBar))
                    {
                        Plugin.Configuration.DotTimerOutlineColor = outlineColor;
                        Plugin.SaveConfiguration();
                    }
                    var thickness = Plugin.Configuration.DotTimerOutlineThickness;
                    if (ImGui.SliderFloat("縁取りの太さ", ref thickness, 0.5f, 3f, "%.1f px"))
                    {
                        Plugin.Configuration.DotTimerOutlineThickness = thickness;
                        Plugin.SaveConfiguration();
                    }
                }
                var iconPosition = Math.Clamp(Plugin.Configuration.DotIconPosition, 0, 2);
                if (ImGui.Combo("表示位置", ref iconPosition, "上\0右\0左\0"))
                {
                    Plugin.Configuration.DotIconPosition = iconPosition;
                    Plugin.SaveConfiguration();
                }
                ImGui.TextUnformatted($"/dl show dot : 表示 {(dotEnabled ? "ON（有効）" : "OFF（無効）")}");
                ImGui.TextUnformatted(Plugin.DotCommandRegistered
                    ? "コマンド: 使用可能（実行するとON/OFFを切り替えます）"
                    : "コマンド: 使用不可（/dl が他のプラグインと競合しています）");
                ImGui.TextDisabled("自分が付与したDoTとデスデザインを表示します。数字は残り秒数です。");
                if (Plugin.DotCatalogError is { } error) ImGui.TextWrapped(error);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Which Activate to PVP"))
            {
                ImGui.TextWrapped("チェックした機能はPvPコンテンツ参加中も使用を許可します。各機能の通常設定も有効である必要があります。");
                ImGui.TextWrapped("チェックを外した機能はPvP参加中のみ停止し、退出後は通常設定に戻ります。ウルヴズジェイル係船場は対象外です。");
                ImGui.TextUnformatted(Plugin.InPvpContent ? "現在: PvP制限を適用中" : "現在: 通常設定を適用中");
                var config = Plugin.Configuration;
                var barrier = config.PvpAllowBarrierHp;
                var search = config.PvpAllowPartySearch;
                var icons = config.PvpAllowChangePartyIcons;
                var dots = config.PvpAllowViewDotIcons;
                var changed = ImGui.Checkbox("Barrier HP", ref barrier);
                changed |= ImGui.Checkbox("PartySearch", ref search);
                changed |= ImGui.Checkbox("Change Party Icons", ref icons);
                changed |= ImGui.Checkbox("View DoT Icons", ref dots);
                if (changed)
                {
                    config.PvpAllowBarrierHp = barrier;
                    config.PvpAllowPartySearch = search;
                    config.PvpAllowChangePartyIcons = icons;
                    config.PvpAllowViewDotIcons = dots;
                    Plugin.SaveConfiguration();
                    Plugin.RequestPartySearchNamePlateRedraw();
                }
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGeneralTab()
    {
        var config = Plugin.Configuration;
        var loggedIn = Plugin.ObjectTable.LocalPlayer != null;
        var inCombat = Plugin.Condition[ConditionFlag.InCombat];
        ImGui.Spacing();
        ImGui.TextUnformatted("便利機能の稼働状況");
        ImGui.TextDisabled("設定と現在の状態を一覧表示します。各機能の設定は該当タブから変更できます。");
        ImGui.TextUnformatted(!loggedIn ? "接続状態：ログイン待ち" : Plugin.InPvpContent
            ? "プレイ状態：PvPコンテンツ参加中" : inCombat ? "プレイ状態：戦闘中" : "プレイ状態：通常");
        ImGui.Spacing();
        if (ImGui.BeginTable("GeneralFeatures", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
            ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("機能", ImGuiTableColumnFlags.WidthStretch, 1.2f);
            ImGui.TableSetupColumn("通常設定", ImGuiTableColumnFlags.WidthStretch, 0.6f);
            ImGui.TableSetupColumn("現在の状態", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("内容", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableHeadersRow();
            Row("Barrier HP", config.ShowBarrierAdjustedHp, Plugin.PvpAllowsBarrierHp, null,
                "パーティリストのHPにバリア量を加算");
            Row("PartySearch", config.PartySearchEnabled, Plugin.PvpAllowsPartySearch,
                inCombat ? "戦闘中は停止" : null, "近隣プレイヤーの名前表示・ターゲット線・招待");
            Row("Change Party Icons", config.ChangePartyIconsEnabled, Plugin.PvpAllowsChangePartyIcons, null,
                "ジョブアイコンをターゲットマーカーに変更");
            Row("View DoT Icons", config.ViewDotIconsEnabled, Plugin.PvpAllowsViewDotIcons,
                Plugin.DotCatalogError != null ? "データ読込エラー" : null, "自分のDoT・デスデザインと残り秒数を表示");
            Row("オリジナルのソート順", config.OriginalTargetMarkerSortEnabled,
                Plugin.PvpAllowsChangePartyIcons, Plugin.SortCommandRegistered ? null : "コマンド競合",
                "/sort tm original で保存した順序を適用");
            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("コマンドガイド");
        ImGui.BulletText("/devlibra：設定画面を開く・閉じる");
        ImGui.BulletText("/dl show dot：DoT表示の有効・無効を切り替え");
        ImGui.BulletText("/sort tm asc / desc / original：マーカー順に並び替え");
        ImGui.BulletText("/sort default：ゲーム標準の順序に並び替え");
        if (!Plugin.DotCommandRegistered) ImGui.TextWrapped("/dl は他のプラグインと競合しています。DoT表示は専用タブから切り替えられます。");
        if (!Plugin.SortCommandRegistered) ImGui.TextWrapped("/sort は他のプラグインと競合しているため使用できません。");
        ImGui.TextDisabled("「利用可能」は設定上の状態です。表示対象が存在する場合に各表示が反映されます。");

        void Row(string name, bool enabled, bool allowed, string? reason, string description)
        {
            var state = !enabled ? "無効" : !loggedIn ? "ログイン待ち" : !allowed ? "PvP設定により停止" : reason ?? "利用可能";
            var active = enabled && loggedIn && allowed && reason == null;
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(name);
            ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted(enabled ? "有効" : "無効");
            ImGui.TableSetColumnIndex(2);
            ImGui.TextColored(active ? new Vector4(0.35f, 0.8f, 1f, 1) : new Vector4(0.60f, 0.69f, 0.8f, 1), state);
            ImGui.TableSetColumnIndex(3); ImGui.TextWrapped(description);
        }
    }

    private unsafe void DrawTargetMarkerSortSettings()
    {
        ImGui.Separator();
        var config = Plugin.Configuration;
        var enabled = config.OriginalTargetMarkerSortEnabled;
        if (ImGui.Checkbox("オリジナルのソート順を有効化", ref enabled))
        {
            config.OriginalTargetMarkerSortEnabled = enabled;
            Plugin.SaveConfiguration();
        }
        if (!enabled) return;

        var direction = config.OriginalTargetMarkerSortDescending ? 1 : 0;
        if (ImGui.Combo("並び順", ref direction, "昇順（上から順）\0降順（下から順）\0"))
        {
            config.OriginalTargetMarkerSortDescending = direction == 1;
            Plugin.SaveConfiguration();
        }
        ImGui.TextUnformatted("ドラッグして優先順位を変更し、/sort tm original で適用します。");
        ImGui.TextDisabled("自分の先頭行と対象外メンバーの位置は維持します。");
        if (ImGui.BeginChild("TargetMarkerOrder", new Vector2(0, 0)))
        {
            var order = config.OriginalTargetMarkerOrder;
            var sourceIndex = -1;
            var destinationIndex = -1;
            for (var i = 0; i < order.Length; i++)
            {
                ImGui.PushID(order[i]);
                var rowPosition = ImGui.GetCursorScreenPos();
                var iconSize = ImGui.GetTextLineHeight() * 1.6f;
                var rowHeight = iconSize + 8f;
                var texture = Plugin.TextureProvider.GetFromGameIcon(TargetMarkerSortOrder.IconId(order[i])).GetWrapOrDefault();
                // The entire row remains one drag source/target, including the icon.
                ImGui.Selectable("##MarkerOrderRow", false, ImGuiSelectableFlags.None, new Vector2(0, rowHeight));
                var drawList = ImGui.GetWindowDrawList();
                if (texture != null)
                    drawList.AddImage(texture.Handle, rowPosition + new Vector2(4, 4),
                        rowPosition + new Vector2(4 + iconSize, 4 + iconSize));
                drawList.AddText(rowPosition + new Vector2(iconSize + 16, (rowHeight - ImGui.GetTextLineHeight()) / 2),
                    ImGui.GetColorU32(ImGuiCol.Text), $"{i + 1}. {TargetMarkerSortOrder.Label(order[i])}");
                if (ImGui.BeginDragDropSource())
                {
                    var marker = order[i];
                    ImGui.SetDragDropPayload("DEVLIBRA_TM_ORDER", new ReadOnlySpan<byte>(&marker, sizeof(int)));
                    if (texture != null)
                    {
                        ImGui.Image(texture.Handle, new Vector2(iconSize));
                        ImGui.SameLine();
                    }
                    ImGui.TextUnformatted(TargetMarkerSortOrder.Label(marker));
                    ImGui.EndDragDropSource();
                }
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("DEVLIBRA_TM_ORDER");
                    if (!payload.IsNull && payload.DataSize == sizeof(int))
                    {
                        sourceIndex = Array.IndexOf(order, *(int*)payload.Data);
                        destinationIndex = i;
                    }
                    ImGui.EndDragDropTarget();
                }
                ImGui.PopID();
            }
            if (sourceIndex >= 0 && destinationIndex >= 0 && sourceIndex != destinationIndex)
            {
                var reordered = order.ToList();
                var marker = reordered[sourceIndex];
                reordered.RemoveAt(sourceIndex);
                reordered.Insert(destinationIndex, marker);
                config.OriginalTargetMarkerOrder = reordered.ToArray();
                Plugin.SaveConfiguration();
            }
        }
        ImGui.EndChild();
    }

    private void DrawBarrierHpTab()
    {
        ImGui.TextDisabled("Auto Make Timeline: Barrier HP IPC v1");
        if (!Plugin.PvpAllowsBarrierHp) ImGui.TextDisabled("PvP設定により現在停止中です。");
        ImGui.TextUnformatted("パーティリストのHP表示");
        ImGui.Separator();

        var showBarrierAdjustedHp = Plugin.Configuration.ShowBarrierAdjustedHp;

        if (ImGui.Checkbox("表示HPにバリア量を加算する", ref showBarrierAdjustedHp))
        {
            Plugin.Configuration.ShowBarrierAdjustedHp = showBarrierAdjustedHp;
            Plugin.SaveConfiguration();
        }

        ImGui.TextDisabled("有効にすると、パーティリストに現在HPとバリア量の合計を表示します。");
        ImGui.TextDisabled("バリア量が含まれている間、HPの数値を緑色で表示します。");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("詳細情報");

        if (Plugin.Condition[ConditionFlag.InCombat])
        {
            ImGui.TextDisabled("戦闘中は詳細情報を表示しません。");
            return;
        }

        ImGui.TextDisabled("付与直後の鼓舞バリアは、観測した回復量の180%として算出します。");

        var debugInfo = Plugin.GetBarrierHpDebugInfo();
        if (debugInfo.Count == 0)
        {
            ImGui.TextDisabled("パーティリストの情報を取得できません。");
            return;
        }

        if (!ImGui.BeginTable(
                "barrierHpDebugTable",
                10,
                ImGuiTableFlags.Borders
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.Resizable
                | ImGuiTableFlags.ScrollX,
                new Vector2(0, 240)))
            return;

        ImGui.TableSetupColumn("表示順");
        ImGui.TableSetupColumn("現在HP");
        ImGui.TableSetupColumn("最大HP");
        ImGui.TableSetupColumn("バリア率");
        ImGui.TableSetupColumn("回復量");
        ImGui.TableSetupColumn("バリア量");
        ImGui.TableSetupColumn("表示HP");
        ImGui.TableSetupColumn("算出方法");
        ImGui.TableSetupColumn("ゲージ最大値");
        ImGui.TableSetupColumn("ゲージ値");
        ImGui.TableHeadersRow();

        foreach (var info in debugInfo)
        {
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(info.PartyIndex.ToString());

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(info.CurrentHp.ToString("N0"));

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(info.MaxHp.ToString("N0"));

            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted($"{info.ShieldPercentage}%");

            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(info.ObservedRecoveryHp.ToString("N0"));

            ImGui.TableSetColumnIndex(5);
            ImGui.TextUnformatted(info.BarrierHp.ToString("N0"));

            ImGui.TableSetColumnIndex(6);
            ImGui.TextUnformatted(info.DisplayHp.ToString("N0"));

            ImGui.TableSetColumnIndex(7);
            ImGui.TextUnformatted(info.CalculationSource);

            ImGui.TableSetColumnIndex(8);
            ImGui.TextUnformatted(info.GaugeMaxValue.ToString("N0"));

            ImGui.TableSetColumnIndex(9);
            ImGui.TextUnformatted($"{info.GaugePrimaryValue:N0} / {info.GaugeSecondaryValue:N0}");
        }

        ImGui.EndTable();
    }

    private void DrawPartySearchTab()
    {
        if (!Plugin.PvpAllowsPartySearch) ImGui.TextDisabled("PvP設定により現在停止中です。");
        ImGui.TextUnformatted("近くにいるソロプレイヤーのコンテンツ参加状態をネームプレートで確認できます。");
        ImGui.TextDisabled("自分以外の100m以内のプレイヤーが対象です。戦闘中は停止します。");
        ImGui.Separator();

        var partySearchEnabled = Plugin.Configuration.PartySearchEnabled;
        if (ImGui.Checkbox("コンテンツ参加状態の表示を有効にする", ref partySearchEnabled))
        {
            Plugin.Configuration.PartySearchEnabled = partySearchEnabled;
            Plugin.SaveConfiguration();
            Plugin.RequestPartySearchNamePlateRedraw();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("対象プレイヤーのネームプレート");

        var displayName = Plugin.Configuration.PartySearchDisplayName;
        ImGui.SetNextItemWidth(320);
        if (ImGui.InputText("置き換える名前", ref displayName, 64))
        {
            Plugin.Configuration.PartySearchDisplayName = displayName;
            Plugin.SaveConfiguration();
            Plugin.RequestPartySearchNamePlateRedraw();
        }

        ImGui.TextDisabled("空欄の場合は元のキャラクター名を表示します。");

        var useCustomColor = Plugin.Configuration.PartySearchUseCustomNameColor;
        if (ImGui.Checkbox("名前の色を変更する", ref useCustomColor))
        {
            Plugin.Configuration.PartySearchUseCustomNameColor = useCustomColor;
            Plugin.SaveConfiguration();
            Plugin.RequestPartySearchNamePlateRedraw();
        }

        if (useCustomColor)
        {
            var nameColor = Plugin.Configuration.PartySearchNameColor;
            ImGui.SetNextItemWidth(260);
            if (ImGui.ColorEdit4("名前の色", ref nameColor, ImGuiColorEditFlags.AlphaBar))
            {
                Plugin.Configuration.PartySearchNameColor = nameColor;
                Plugin.SaveConfiguration();
                Plugin.RequestPartySearchNamePlateRedraw();
            }
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("対象プレイヤーへのターゲット線");

        var drawTargetLines = Plugin.Configuration.PartySearchDrawTargetLines;
        if (ImGui.Checkbox("自分から対象プレイヤーへ線を表示する", ref drawTargetLines))
        {
            Plugin.Configuration.PartySearchDrawTargetLines = drawTargetLines;
            Plugin.SaveConfiguration();
        }

        if (drawTargetLines)
        {
            var lineColor = Plugin.Configuration.PartySearchTargetLineColor;
            ImGui.SetNextItemWidth(260);
            if (ImGui.ColorEdit4("線の色", ref lineColor, ImGuiColorEditFlags.AlphaBar))
            {
                Plugin.Configuration.PartySearchTargetLineColor = lineColor;
                Plugin.SaveConfiguration();
            }

            var lineThickness = Plugin.Configuration.PartySearchTargetLineThickness;
            ImGui.SetNextItemWidth(260);
            if (ImGui.SliderFloat("線の太さ", ref lineThickness, 1f, 10f, "%.1f px"))
            {
                Plugin.Configuration.PartySearchTargetLineThickness = lineThickness;
                Plugin.SaveConfiguration();
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled(
            !Plugin.PvpAllowsPartySearch || !Plugin.Configuration.PartySearchEnabled
                ? "停止中：設定で無効になっています。"
                : Plugin.Condition[ConditionFlag.InCombat]
                ? "停止中：戦闘中です。"
                : "有効：100m以内のプレイヤーを確認しています。");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("100m以内のプレイヤー");
        ImGui.TextDisabled("黄色のコンテンツ参加アイコンが表示されているプレイヤーが対象です。");

        var nearbyPlayers = Plugin.GetPartySearchNearbyPlayers();
        if (nearbyPlayers.Count == 0)
        {
            ImGui.TextDisabled("100m以内に他のプレイヤーはいません。");
            return;
        }

        if (!ImGui.BeginTable(
                "partySearchNearbyPlayers",
                4,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable,
                new Vector2(0, 240)))
            return;

        ImGui.TableSetupColumn("プレイヤー");
        ImGui.TableSetupColumn("距離", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("状態", ImGuiTableColumnFlags.WidthFixed, 160);
        ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableHeadersRow();

        var canInvite = !Plugin.Condition[ConditionFlag.InCombat];
        foreach (var entry in nearbyPlayers)
        {
            var player = entry.Player;
            var canInvitePlayer = canInvite
                && entry.HasNameplateStatus
                && entry.IsContentParticipant
                && Plugin.CanInviteToParty(player);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(player.Name.TextValue);

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted($"{player.CurrentDistance}m");

            ImGui.TableSetColumnIndex(2);
            if (!entry.HasNameplateStatus)
                ImGui.TextDisabled("ネームプレート未表示");
            else if (!entry.IsContentParticipant)
                ImGui.TextDisabled("コンテンツ未参加");
            else
                ImGui.TextUnformatted("コンテンツ参加中");

            ImGui.TableSetColumnIndex(3);
            ImGui.BeginDisabled(!canInvitePlayer);
            if (ImGui.Button($"招待##{player.GameObjectId}"))
            {
                this.partyInviteTargetGameObjectId = player.GameObjectId;
                Plugin.InviteToParty(player);
            }
            ImGui.EndDisabled();
        }

        ImGui.EndTable();

        if (this.partyInviteTargetGameObjectId != 0)
        {
            var partyInviteResult = Plugin.GetPartyInviteResult(this.partyInviteTargetGameObjectId);
            if (!string.IsNullOrEmpty(partyInviteResult))
                ImGui.TextWrapped(partyInviteResult);
        }
    }

    private void DrawPartyMemberTab()
    {
        ImGui.TextUnformatted("周囲のプレイヤーに現在付与されているバフ・デバフ情報を表示します。");
        ImGui.TextDisabled("※ リプレイ確認を前提に、PartyListではなく周囲のIPlayerCharacterを全取得します。");
        ImGui.Separator();

        var players = this.GetReplayPlayerCharacters();

        if (players.Count == 0)
        {
            ImGui.TextUnformatted("周囲にプレイヤー情報が見つかりません。");
            ImGui.TextDisabled("リプレイ再生中のキャラクターがObjectTableに出ていない可能性があります。");
            return;
        }

        ImGui.TextUnformatted($"取得対象: {players.Count} 人");
        ImGui.Separator();

        if (ImGui.BeginTable(
                "partyMemberStatusTable",
                8,
                ImGuiTableFlags.Borders
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.Resizable
                | ImGuiTableFlags.ScrollY,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("メンバー");
            ImGui.TableSetupColumn("ジョブ");
            ImGui.TableSetupColumn("ステータスID");
            ImGui.TableSetupColumn("ステータス名");
            ImGui.TableSetupColumn("パラメーター");
            ImGui.TableSetupColumn("残り秒数");
            ImGui.TableSetupColumn("付与者ID");
            ImGui.TableSetupColumn("番号");
            ImGui.TableHeadersRow();

            foreach (var player in players)
            {
                var memberName = player.Name.TextValue;
                var job = player.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? "-";
                var statuses = player.StatusList;

                var hasStatus = false;

                for (var i = 0; i < statuses.Length; i++)
                {
                    var status = statuses[i];

                    if (status.StatusId == 0)
                        continue;

                    hasStatus = true;

                    this.DrawPartyStatusRow(
                        memberName,
                        job,
                        status,
                        i);
                }

                if (!hasStatus)
                {
                    this.DrawPartyMemberNoStatusRow(
                        memberName,
                        job);
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawEnemyCastingTab()
    {
        ImGui.TextUnformatted("エネミーが現在詠唱中の攻撃情報を表示します。");
        ImGui.Separator();

        if (ImGui.BeginTable(
                "enemyCastingTable",
                11,
                ImGuiTableFlags.Borders
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.Resizable
                | ImGuiTableFlags.ScrollY,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("敵");
            ImGui.TableSetupColumn("エンティティID");
            ImGui.TableSetupColumn("オブジェクトID");
            ImGui.TableSetupColumn("アクションID");
            ImGui.TableSetupColumn("アクション名");
            ImGui.TableSetupColumn("詠唱時間");
            ImGui.TableSetupColumn("詠唱経過");
            ImGui.TableSetupColumn("詠唱全体");
            ImGui.TableSetupColumn("ステータスID");
            ImGui.TableSetupColumn("パラメーター");
            ImGui.TableSetupColumn("ステータス番号");
            ImGui.TableHeadersRow();

            var castingEnemies = this.GetEnemyBattleCharas()
                .Where(battleChara => battleChara.IsCasting)
                .ToList();

            if (castingEnemies.Count == 0)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextDisabled("現在詠唱中のエネミーはいません。");

                ImGui.EndTable();
                return;
            }

            foreach (var battleChara in castingEnemies)
            {
                var enemyName = battleChara.Name.TextValue;
                var entityId = battleChara.EntityId;
                var objectId = battleChara.GameObjectId;
                var actionId = battleChara.CastActionId;
                var actionName = this.GetActionName(actionId);
                var currentCast = battleChara.CurrentCastTime;
                var totalCast = battleChara.TotalCastTime;

                var statuses = battleChara.StatusList;
                var hasStatus = false;

                for (var i = 0; i < statuses.Length; i++)
                {
                    var status = statuses[i];

                    if (status.StatusId == 0)
                        continue;

                    hasStatus = true;

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextUnformatted(enemyName);

                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(entityId.ToString());

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted(objectId.ToString());

                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted(actionId.ToString());

                    ImGui.TableSetColumnIndex(4);
                    ImGui.TextUnformatted(actionName);

                    ImGui.TableSetColumnIndex(5);
                    ImGui.TextUnformatted($"{currentCast:0.00} / {totalCast:0.00}");

                    ImGui.TableSetColumnIndex(6);
                    ImGui.TextUnformatted($"{currentCast:0.00}");

                    ImGui.TableSetColumnIndex(7);
                    ImGui.TextUnformatted($"{totalCast:0.00}");

                    ImGui.TableSetColumnIndex(8);
                    ImGui.TextUnformatted(status.StatusId.ToString());

                    ImGui.TableSetColumnIndex(9);
                    ImGui.TextUnformatted(status.Param.ToString());

                    ImGui.TableSetColumnIndex(10);
                    ImGui.TextUnformatted(i.ToString());
                }

                if (!hasStatus)
                {
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextUnformatted(enemyName);

                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(entityId.ToString());

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted(objectId.ToString());

                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted(actionId.ToString());

                    ImGui.TableSetColumnIndex(4);
                    ImGui.TextUnformatted(actionName);

                    ImGui.TableSetColumnIndex(5);
                    ImGui.TextUnformatted($"{currentCast:0.00} / {totalCast:0.00}");

                    ImGui.TableSetColumnIndex(6);
                    ImGui.TextUnformatted($"{currentCast:0.00}");

                    ImGui.TableSetColumnIndex(7);
                    ImGui.TextUnformatted($"{totalCast:0.00}");

                    ImGui.TableSetColumnIndex(8);
                    ImGui.TextDisabled("ステータスなし");

                    ImGui.TableSetColumnIndex(9);
                    ImGui.TextDisabled("-");

                    ImGui.TableSetColumnIndex(10);
                    ImGui.TextDisabled("-");
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawEnemyStatusTab()
    {
        ImGui.TextUnformatted("周囲のエネミーに現在付与されているバフ・デバフ情報を表示します。");
        ImGui.TextDisabled("※ BattleNpc の StatusList を表示します。リプレイ確認用です。");
        ImGui.Separator();

        var enemies = this.GetEnemyBattleCharas();

        if (enemies.Count == 0)
        {
            ImGui.TextUnformatted("周囲にエネミー情報が見つかりません。");
            return;
        }

        ImGui.TextUnformatted($"取得対象: {enemies.Count} 体");
        ImGui.Separator();

        if (ImGui.BeginTable(
                "enemyStatusTable",
                9,
                ImGuiTableFlags.Borders
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.Resizable
                | ImGuiTableFlags.ScrollY,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("敵");
            ImGui.TableSetupColumn("エンティティID");
            ImGui.TableSetupColumn("オブジェクトID");
            ImGui.TableSetupColumn("ステータスID");
            ImGui.TableSetupColumn("ステータス名");
            ImGui.TableSetupColumn("パラメーター");
            ImGui.TableSetupColumn("残り秒数");
            ImGui.TableSetupColumn("付与者ID");
            ImGui.TableSetupColumn("番号");
            ImGui.TableHeadersRow();

            foreach (var enemy in enemies)
            {
                var enemyName = enemy.Name.TextValue;
                var entityId = enemy.EntityId;
                var objectId = enemy.GameObjectId;
                var statuses = enemy.StatusList;

                var hasStatus = false;

                for (var i = 0; i < statuses.Length; i++)
                {
                    var status = statuses[i];

                    if (status.StatusId == 0)
                        continue;

                    hasStatus = true;

                    this.DrawEnemyStatusRow(
                        enemyName,
                        entityId,
                        objectId,
                        status,
                        i);
                }

                if (!hasStatus)
                {
                    this.DrawEnemyNoStatusRow(
                        enemyName,
                        entityId,
                        objectId);
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawStatusSearchTab()
    {
        ImGui.TextUnformatted("全ステータスID・名前 からステータス情報を検索します。");
        ImGui.TextDisabled("※ 現在付与されているステータスではなく、LuminaのStatusシート全体から検索します。");
        ImGui.TextDisabled("※ パラメーターは付与中のステータス固有の値のため、全件検索では「-」と表示します。");
        ImGui.Separator();

        ImGui.SetNextItemWidth(300);
        ImGui.InputText("ステータスID / 名前", ref this.statusSearchText, 128);

        ImGui.SameLine();

        ImGui.Checkbox("完全一致", ref this.statusSearchExactMatch);

        var searchMode = this.statusSearchExactMatch ? "完全一致" : "部分一致";
        ImGui.TextDisabled($"検索方法: {searchMode}");

        ImGui.Separator();

        var searchText = this.statusSearchText.Trim();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ImGui.TextUnformatted("ステータスIDまたは名前を入力してください。");
            ImGui.TextDisabled("例：5547 / 混沌の炎 / 被ダメージ上昇 など");
            return;
        }

        var results = this.SearchAllStatuses(searchText, this.statusSearchExactMatch);

        ImGui.TextUnformatted($"検索結果: {results.Count} 件");
        ImGui.Separator();

        if (results.Count == 0)
        {
            ImGui.TextDisabled("一致するステータスは見つかりませんでした。");
            return;
        }

        if (ImGui.BeginTable(
                "statusSearchResultTable",
                8,
                ImGuiTableFlags.Borders
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.Resizable
                | ImGuiTableFlags.ScrollY,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("アイコン");
            ImGui.TableSetupColumn("アイコンID");
            ImGui.TableSetupColumn("ステータスID");
            ImGui.TableSetupColumn("ステータス名");
            ImGui.TableSetupColumn("パラメーター");
            ImGui.TableSetupColumn("説明");
            ImGui.TableSetupColumn("解除可能");
            ImGui.TableSetupColumn("最大スタック");
            ImGui.TableHeadersRow();

            foreach (var result in results)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                this.DrawStatusIcon(result.IconId);

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(result.IconId.ToString());

                ImGui.TableSetColumnIndex(2);
                ImGui.TextUnformatted(result.StatusId.ToString());

                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted(result.StatusName);

                ImGui.TableSetColumnIndex(4);
                ImGui.TextUnformatted(result.Param);

                ImGui.TableSetColumnIndex(5);
                ImGui.TextWrapped(result.Description);

                ImGui.TableSetColumnIndex(6);
                ImGui.TextUnformatted(result.CanDispel ? "可能" : "不可");

                ImGui.TableSetColumnIndex(7);
                ImGui.TextUnformatted(result.MaxStacks.ToString());
            }

            ImGui.EndTable();
        }
    }

    private void DrawActionSearchTab()
    {
        ImGui.TextUnformatted("全アクションID・名前 からアクション情報を検索します。");
        ImGui.TextDisabled("※ ゲームデータに登録されているすべてのアクションを検索します。");
        ImGui.Separator();

        ImGui.SetNextItemWidth(300);
        ImGui.InputText("アクションID / 名前", ref this.actionSearchText, 128);

        ImGui.SameLine();

        ImGui.Checkbox("完全一致", ref this.actionSearchExactMatch);

        var searchMode = this.actionSearchExactMatch ? "完全一致" : "部分一致";
        ImGui.TextDisabled($"検索方法: {searchMode}");

        ImGui.Separator();

        var searchText = this.actionSearchText.Trim();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ImGui.TextUnformatted("アクションIDまたは名前を入力してください。");
            ImGui.TextDisabled("例：47764 / なぞなぞマジック / ファイア / ブリザド など");
            return;
        }

        var results = this.SearchAllActions(searchText, this.actionSearchExactMatch);

        ImGui.TextUnformatted($"検索結果: {results.Count} 件");
        ImGui.Separator();

        if (results.Count == 0)
        {
            ImGui.TextDisabled("一致するアクションは見つかりませんでした。");
            return;
        }

        if (ImGui.BeginTable(
                "actionSearchResultTable",
                2,
                ImGuiTableFlags.Borders
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.Resizable
                | ImGuiTableFlags.ScrollY,
                new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("アクションID");
            ImGui.TableSetupColumn("アクション名");
            ImGui.TableHeadersRow();

            foreach (var result in results)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(result.ActionId.ToString());

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(result.ActionName);
            }

            ImGui.EndTable();
        }
    }

    private List<IPlayerCharacter> GetReplayPlayerCharacters()
    {
        return Plugin.ObjectTable
            .Where(obj => obj is IPlayerCharacter)
            .Cast<IPlayerCharacter>()
            .Where(player => !string.IsNullOrWhiteSpace(player.Name.TextValue))
            .OrderBy(player => player.Name.TextValue, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.EntityId)
            .ToList();
    }

    private List<IBattleChara> GetEnemyBattleCharas()
    {
        return Plugin.ObjectTable
            .Where(obj => obj is IBattleChara)
            .Cast<IBattleChara>()
            .Where(battleChara => battleChara.ObjectKind == ObjectKind.BattleNpc)
            .Where(battleChara => !string.IsNullOrWhiteSpace(battleChara.Name.TextValue))
            .OrderBy(battleChara => battleChara.Name.TextValue, StringComparer.OrdinalIgnoreCase)
            .ThenBy(battleChara => battleChara.EntityId)
            .ToList();
    }

    private List<StatusSearchResult> SearchAllStatuses(
        string searchText,
        bool exactMatch)
    {
        var results = new List<StatusSearchResult>();

        try
        {
            var statusSheet = Plugin.DataManager.GetExcelSheet<LuminaStatus>();

            foreach (var status in statusSheet)
            {
                if (status.RowId == 0)
                    continue;

                var statusIdText = status.RowId.ToString();
                var statusName = status.Name.ExtractText();

                if (string.IsNullOrWhiteSpace(statusName))
                    continue;

                if (!this.IsMatched(statusIdText, statusName, searchText, exactMatch))
                    continue;

                results.Add(new StatusSearchResult
                {
                    StatusId = status.RowId,
                    StatusName = statusName,
                    IconId = status.Icon,
                    Param = "-",
                    Description = status.Description.ExtractText(),
                    CanDispel = status.CanDispel,
                    MaxStacks = status.MaxStacks
                });
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to search all statuses.");
        }

        return results
            .OrderBy(result => result.StatusId)
            .ToList();
    }

    private List<ActionSearchResult> SearchAllActions(
        string searchText,
        bool exactMatch)
    {
        var results = new List<ActionSearchResult>();

        try
        {
            var actionSheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();

            foreach (var action in actionSheet)
            {
                if (action.RowId == 0)
                    continue;

                var actionIdText = action.RowId.ToString();
                var actionName = action.Name.ExtractText();

                if (string.IsNullOrWhiteSpace(actionName))
                    continue;

                if (!this.IsMatched(actionIdText, actionName, searchText, exactMatch))
                    continue;

                results.Add(new ActionSearchResult
                {
                    ActionId = action.RowId,
                    ActionName = actionName
                });
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to search all actions.");
        }

        return results
            .OrderBy(result => result.ActionId)
            .ToList();
    }

    private bool IsMatched(
        string idText,
        string name,
        string searchText,
        bool exactMatch)
    {
        if (exactMatch)
        {
            return string.Equals(idText, searchText, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, searchText, StringComparison.OrdinalIgnoreCase);
        }

        return idText.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawPartyStatusRow(
        string memberName,
        string job,
        IStatus status,
        int index)
    {
        var statusName = this.GetStatusName(status.StatusId);

        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(memberName);

        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(job);

        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(status.StatusId.ToString());

        ImGui.TableSetColumnIndex(3);
        ImGui.TextUnformatted(statusName);

        ImGui.TableSetColumnIndex(4);
        ImGui.TextUnformatted(status.Param.ToString());

        ImGui.TableSetColumnIndex(5);
        ImGui.TextUnformatted($"{status.RemainingTime:0.00}");

        ImGui.TableSetColumnIndex(6);
        ImGui.TextUnformatted(status.SourceId.ToString());

        ImGui.TableSetColumnIndex(7);
        ImGui.TextUnformatted(index.ToString());
    }

    private void DrawPartyMemberNoStatusRow(
        string memberName,
        string job)
    {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(memberName);

        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(job);

        ImGui.TableSetColumnIndex(2);
        ImGui.TextDisabled("ステータスなし");

        ImGui.TableSetColumnIndex(3);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(4);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(5);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(6);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(7);
        ImGui.TextDisabled("-");
    }

    private void DrawEnemyStatusRow(
        string enemyName,
        uint entityId,
        ulong objectId,
        IStatus status,
        int index)
    {
        var statusName = this.GetStatusName(status.StatusId);

        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(enemyName);

        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(entityId.ToString());

        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(objectId.ToString());

        ImGui.TableSetColumnIndex(3);
        ImGui.TextUnformatted(status.StatusId.ToString());

        ImGui.TableSetColumnIndex(4);
        ImGui.TextUnformatted(statusName);

        ImGui.TableSetColumnIndex(5);
        ImGui.TextUnformatted(status.Param.ToString());

        ImGui.TableSetColumnIndex(6);
        ImGui.TextUnformatted($"{status.RemainingTime:0.00}");

        ImGui.TableSetColumnIndex(7);
        ImGui.TextUnformatted(status.SourceId.ToString());

        ImGui.TableSetColumnIndex(8);
        ImGui.TextUnformatted(index.ToString());
    }

    private void DrawEnemyNoStatusRow(
        string enemyName,
        uint entityId,
        ulong objectId)
    {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(enemyName);

        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(entityId.ToString());

        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(objectId.ToString());

        ImGui.TableSetColumnIndex(3);
        ImGui.TextDisabled("ステータスなし");

        ImGui.TableSetColumnIndex(4);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(5);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(6);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(7);
        ImGui.TextDisabled("-");

        ImGui.TableSetColumnIndex(8);
        ImGui.TextDisabled("-");
    }

    private void DrawStatusIcon(uint iconId)
    {
        if (iconId == 0)
        {
            ImGui.TextDisabled("-");
            return;
        }

        try
        {
            var textureWrap = Plugin.TextureProvider
                .GetFromGameIcon(iconId)
                .GetWrapOrDefault();

            if (textureWrap == null)
            {
                ImGui.TextDisabled(iconId.ToString());
                return;
            }

            ImGui.Image(textureWrap.Handle, new Vector2(32, 32));
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, $"Failed to draw status icon. IconId={iconId}");
            ImGui.TextDisabled(iconId.ToString());
        }
    }

    private string GetActionName(uint actionId)
    {
        if (actionId == 0)
            return "-";

        try
        {
            var actionSheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();
            var action = actionSheet.GetRow(actionId);

            var actionName = action.Name.ExtractText();

            if (string.IsNullOrWhiteSpace(actionName))
                return "-";

            return actionName;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, $"Failed to get action name. ActionId={actionId}");
            return "-";
        }
    }

    private string GetStatusName(uint statusId)
    {
        if (statusId == 0)
            return "-";

        try
        {
            var statusSheet = Plugin.DataManager.GetExcelSheet<LuminaStatus>();
            var status = statusSheet.GetRow(statusId);

            var statusName = status.Name.ExtractText();

            if (string.IsNullOrWhiteSpace(statusName))
                return "-";

            return statusName;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, $"Failed to get status name. StatusId={statusId}");
            return "-";
        }
    }

    private sealed class StatusSearchResult
    {
        public uint StatusId { get; init; }

        public string StatusName { get; init; } = string.Empty;

        public uint IconId { get; init; }

        public string Param { get; init; } = "-";

        public string Description { get; init; } = string.Empty;

        public bool CanDispel { get; init; }

        public byte MaxStacks { get; init; }
    }

    private sealed class ActionSearchResult
    {
        public uint ActionId { get; init; }

        public string ActionName { get; init; } = string.Empty;
    }
}



