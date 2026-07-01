using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
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

    public MainWindow()
        : base(
            "devLibra",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(1400, 800);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("devLibraTabs"))
        {
            if (ImGui.BeginTabItem("PartyMember"))
            {
                this.DrawPartyMemberTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("EnemyCasting"))
            {
                this.DrawEnemyCastingTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("EnemyStatus"))
            {
                this.DrawEnemyStatusTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("StatusSearch"))
            {
                this.DrawStatusSearchTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("ActionSearch"))
            {
                this.DrawActionSearchTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawPartyMemberTab()
    {
        ImGui.TextUnformatted("ObjectTable上のプレイヤーに現在付与されているバフ・デバフ情報を表示します。");
        ImGui.TextDisabled("※ リプレイ確認を前提に、PartyListではなくObjectTable上のIPlayerCharacterを全取得します。");
        ImGui.Separator();

        var players = this.GetReplayPlayerCharacters();

        if (players.Count == 0)
        {
            ImGui.TextUnformatted("ObjectTable上にプレイヤー情報が見つかりません。");
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
            ImGui.TableSetupColumn("Member");
            ImGui.TableSetupColumn("Job");
            ImGui.TableSetupColumn("StatusId");
            ImGui.TableSetupColumn("StatusName");
            ImGui.TableSetupColumn("Param");
            ImGui.TableSetupColumn("Remaining");
            ImGui.TableSetupColumn("SourceId");
            ImGui.TableSetupColumn("Index");
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
            ImGui.TableSetupColumn("Enemy");
            ImGui.TableSetupColumn("EntityId");
            ImGui.TableSetupColumn("ObjectId");
            ImGui.TableSetupColumn("ActionId");
            ImGui.TableSetupColumn("ActionName");
            ImGui.TableSetupColumn("CastTime");
            ImGui.TableSetupColumn("CastCurrent");
            ImGui.TableSetupColumn("CastTotal");
            ImGui.TableSetupColumn("StatusId");
            ImGui.TableSetupColumn("Param");
            ImGui.TableSetupColumn("StatusIndex");
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
                    ImGui.TextDisabled("No Status");

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
        ImGui.TextUnformatted("ObjectTable上のエネミーに現在付与されているバフ・デバフ情報を表示します。");
        ImGui.TextDisabled("※ BattleNpc の StatusList を表示します。リプレイ確認用です。");
        ImGui.Separator();

        var enemies = this.GetEnemyBattleCharas();

        if (enemies.Count == 0)
        {
            ImGui.TextUnformatted("ObjectTable上にエネミー情報が見つかりません。");
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
            ImGui.TableSetupColumn("Enemy");
            ImGui.TableSetupColumn("EntityId");
            ImGui.TableSetupColumn("ObjectId");
            ImGui.TableSetupColumn("StatusId");
            ImGui.TableSetupColumn("StatusName");
            ImGui.TableSetupColumn("Param");
            ImGui.TableSetupColumn("Remaining");
            ImGui.TableSetupColumn("SourceId");
            ImGui.TableSetupColumn("Index");
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
        ImGui.TextUnformatted("全StatusId / StatusName からステータス情報を検索します。");
        ImGui.TextDisabled("※ 現在付与されているステータスではなく、LuminaのStatusシート全体から検索します。");
        ImGui.TextDisabled("※ Paramは付与中ステータスにだけ存在する値のため、全Status検索では '-' 表示です。");
        ImGui.Separator();

        ImGui.SetNextItemWidth(300);
        ImGui.InputText("StatusId / StatusName", ref this.statusSearchText, 128);

        ImGui.SameLine();

        ImGui.Checkbox("完全一致", ref this.statusSearchExactMatch);

        var searchMode = this.statusSearchExactMatch ? "完全一致" : "部分一致";
        ImGui.TextDisabled($"検索方法: {searchMode}");

        ImGui.Separator();

        var searchText = this.statusSearchText.Trim();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ImGui.TextUnformatted("StatusId または StatusName を入力してください。");
            ImGui.TextDisabled("例: 5547 / 混沌の炎 / Vulnerability / Down など");
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
            ImGui.TableSetupColumn("Icon");
            ImGui.TableSetupColumn("IconId");
            ImGui.TableSetupColumn("StatusId");
            ImGui.TableSetupColumn("StatusName");
            ImGui.TableSetupColumn("Param");
            ImGui.TableSetupColumn("Description");
            ImGui.TableSetupColumn("CanDispel");
            ImGui.TableSetupColumn("MaxStacks");
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
                ImGui.TextUnformatted(result.CanDispel ? "true" : "false");

                ImGui.TableSetColumnIndex(7);
                ImGui.TextUnformatted(result.MaxStacks.ToString());
            }

            ImGui.EndTable();
        }
    }

    private void DrawActionSearchTab()
    {
        ImGui.TextUnformatted("全ActionId / ActionName からアクション情報を検索します。");
        ImGui.TextDisabled("※ 現在詠唱中のActionではなく、LuminaのActionシート全体から検索します。");
        ImGui.Separator();

        ImGui.SetNextItemWidth(300);
        ImGui.InputText("ActionId / ActionName", ref this.actionSearchText, 128);

        ImGui.SameLine();

        ImGui.Checkbox("完全一致", ref this.actionSearchExactMatch);

        var searchMode = this.actionSearchExactMatch ? "完全一致" : "部分一致";
        ImGui.TextDisabled($"検索方法: {searchMode}");

        ImGui.Separator();

        var searchText = this.actionSearchText.Trim();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ImGui.TextUnformatted("ActionId または ActionName を入力してください。");
            ImGui.TextDisabled("例: 47764 / なぞなぞマジック / Fire / Blizzard など");
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
            ImGui.TableSetupColumn("ActionId");
            ImGui.TableSetupColumn("ActionName");
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
        ImGui.TextDisabled("No Status");

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
        ImGui.TextDisabled("No Status");

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