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
    public MainWindow()
        : base(
            "devLibra",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(1200, 700);
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

            var castingEnemies = Plugin.ObjectTable
                .Where(obj => obj is IBattleChara)
                .Cast<IBattleChara>()
                .Where(battleChara => battleChara.ObjectKind == ObjectKind.BattleNpc)
                .Where(battleChara => battleChara.IsCasting)
                .OrderBy(battleChara => battleChara.Name.TextValue, StringComparer.OrdinalIgnoreCase)
                .ThenBy(battleChara => battleChara.EntityId)
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
}