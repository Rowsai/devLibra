using System;
using System.Linq;
using System.Threading;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace devLibra;

internal sealed unsafe class PartyListSorter : IDisposable
{
    private const string OriginalDisabled = "[devLibra]オリジナルのソート順が有効化されていません。";
    private readonly bool commandRegistered;
    private int pendingMode;
    private ulong[]? expectedOrder;
    private long verificationDeadline;

    public PartyListSorter()
    {
        commandRegistered = Plugin.CommandManager.AddHandler("/sort", new CommandInfo(OnCommand)
        {
            HelpMessage = "パーティ並び替え: /sort tm asc | tm desc | tm original | default",
        });
        if (!commandRegistered)
            Plugin.ChatGui.PrintError("[devLibra]/sort は他のプラグインに登録されているため使用できません。");
    }

    private void OnCommand(string command, string arguments)
    {
        var mode = TargetMarkerSortOrder.Parse(arguments);
        if (mode == PartySortMode.None)
        {
            Plugin.ChatGui.PrintError("[devLibra]使い方: /sort tm asc | /sort tm desc | /sort tm original | /sort default");
            return;
        }
        if (mode == PartySortMode.Original && !Plugin.Configuration.OriginalTargetMarkerSortEnabled)
        {
            Plugin.ChatGui.PrintError(OriginalDisabled);
            return;
        }
        // Native UI operations run on Framework.Update, not in a chat callback.
        Interlocked.Exchange(ref pendingMode, (int)mode);
    }

    public void Update()
    {
        try
        {
            var mode = (PartySortMode)Interlocked.Exchange(ref pendingMode, 0);
            if (mode != PartySortMode.None)
            {
                expectedOrder = null;
                Execute(mode);
            }
            else if (expectedOrder != null)
            {
                if (TrySnapshot(out var actual, out _) && actual.SequenceEqual(expectedOrder))
                    expectedOrder = null;
                else if (Environment.TickCount64 >= verificationDeadline)
                {
                    expectedOrder = null;
                    Plugin.ChatGui.PrintError("[devLibra]並び替え結果を確認できませんでした。パーティの状態を確認して再実行してください。");
                }
            }
        }
        catch (Exception ex)
        {
            expectedOrder = null;
            Plugin.Log.Error(ex, "Party list sorting failed.");
            Plugin.ChatGui.PrintError("[devLibra]パーティの並び替えに失敗しました。");
        }
    }

    private void Execute(PartySortMode mode)
    {
        if (mode == PartySortMode.Original && !Plugin.Configuration.OriginalTargetMarkerSortEnabled)
        {
            Plugin.ChatGui.PrintError(OriginalDisabled);
            return;
        }
        if (Plugin.ObjectTable.LocalPlayer == null)
        {
            Plugin.ChatGui.PrintError("[devLibra]ログイン中に実行してください。");
            return;
        }
        if (mode == PartySortMode.Default)
        {
            var ui = UIModule.Instance();
            if (ui == null) return;
            var text = Utf8String.FromString("/partysort");
            try { ui->ProcessChatBoxEntry(text); }
            finally { text->Dtor(true); }
            return;
        }

        if (!TrySnapshot(out var current, out var markers))
        {
            Plugin.ChatGui.PrintError("[devLibra]並び替え可能な通常パーティリストを取得できませんでした。");
            return;
        }
        var config = Plugin.Configuration;
        var order = mode == PartySortMode.Original
            ? TargetMarkerSortOrder.Normalize(config.OriginalTargetMarkerOrder)
            : TargetMarkerSortOrder.DefaultOrder();
        var descending = mode == PartySortMode.Descending ||
            (mode == PartySortMode.Original && config.OriginalTargetMarkerSortDescending);
        var target = TargetMarkerSortOrder.BuildTarget(markers, order, descending);
        var swaps = TargetMarkerSortOrder.BuildSwaps(target);
        if (swaps.Count == 0) return;
        var proxy = InfoProxyPartyMember.Instance();
        if (proxy == null)
        {
            Plugin.ChatGui.PrintError("[devLibra]パーティの並び替え機能を取得できませんでした。");
            return;
        }
        foreach (var (first, second) in swaps) proxy->ChangeOrder(first, second, true);
        expectedOrder = target.Select(row => current[row]).ToArray();
        verificationDeadline = Environment.TickCount64 + 1500;
    }

    private static bool TrySnapshot(out ulong[] identities, out int[] markers)
    {
        identities = [];
        markers = [];
        var hud = AgentHUD.Instance();
        var addon = Plugin.GameGui.GetAddonByName<AddonPartyList>("_PartyList");
        var marking = MarkingController.Instance();
        var local = Plugin.ObjectTable.LocalPlayer;
        var count = Plugin.PartyList.Length;
        if (hud == null || addon == null || marking == null || local == null ||
            count < 2 || count > 8 || hud->PartyMemberCount != count || addon->MemberCount != count)
            return false;
        identities = new ulong[count];
        markers = Enumerable.Repeat(-1, count).ToArray();
        var selfFound = false;
        for (var i = 0; i < count; i++)
        {
            var member = hud->PartyMembers[i];
            var row = member.Index;
            if (row >= count || identities[row] != 0) return false;
            var identity = member.ContentId;
            if (identity == 0) return false;
            identities[row] = identity;
            if (member.EntityId == local.EntityId)
            {
                if (row != 0) return false;
                selfFound = true;
            }
            if (member.EntityId is 0 or 0xE0000000) continue;
            for (var marker = 0; marker < marking->Markers.Length; marker++)
            {
                var assigned = marking->Markers[marker];
                if (assigned.Type == 0 && assigned.ObjectId == member.EntityId)
                {
                    markers[row] = marker;
                    break;
                }
            }
        }
        return selfFound && identities.All(id => id != 0) && identities.Distinct().Count() == count;
    }

    public void Dispose()
    {
        if (commandRegistered) Plugin.CommandManager.RemoveHandler("/sort");
        Interlocked.Exchange(ref pendingMode, 0);
        expectedOrder = null;
    }
}
