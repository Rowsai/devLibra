using System;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace devLibra;

/// <summary>Projects the native enemy-party order onto configured macro render intermediates.</summary>
internal sealed unsafe class PvpBattleMacroIcons : IDisposable
{
    private delegate void PrepareSlotDelegate(RaptureHotbarModule* module, HotbarSlot* slot, HotbarUIIntermediate* output);
    private Hook<PrepareSlotDelegate>? renderHook;
    private readonly uint[] jobIcons = new uint[5];
    private readonly string[] enemyLabels = new string[5];
    private long nextUpdate;
    private long nextLog;
    private ulong owner;
    private uint territory;
    internal string? Error { get; private set; }

    internal static bool InCrystallineConflict
    {
        get
        {
            if (!Plugin.InPvpContent || Plugin.ObjectTable.LocalPlayer == null) return false;
            var framework = EventFramework.Instance();
            var director = framework == null ? null : framework->GetInstanceContentDirector();
            return director != null && director->InstanceContentType == InstanceContentType.CrystallineConflict;
        }
    }
    private bool Enabled => Error == null && Plugin.Configuration.ForPvpBattleEnabled && InCrystallineConflict;
    internal string EnemyLabel(int row) => Enabled && row >= 0 && row < 5 ? enemyLabels[row] ?? "取得待ち" : "取得待ち";

    internal PvpBattleMacroIcons()
    {
        try
        {
            renderHook = Plugin.InteropProvider.HookFromAddress<PrepareSlotDelegate>((nint)RaptureHotbarModule.MemberFunctionPointers.PrepareSlotForRender, PrepareSlot);
            renderHook.Enable();
        }
        catch (Exception ex)
        {
            Dispose();
            Error = "ゲーム処理に接続できませんでした。プラグインの更新を確認してください。";
            Plugin.Log.Error(ex, "For PvP Battle hook initialization failed.");
        }
    }

    internal void Update()
    {
        try
        {
            var currentOwner = Plugin.ObjectTable.LocalPlayer?.GameObjectId ?? 0;
            if (!Enabled || owner != currentOwner || territory != Plugin.ClientState.TerritoryType)
            {
                Array.Clear(jobIcons); Array.Clear(enemyLabels);
                owner = currentOwner; territory = Plugin.ClientState.TerritoryType;
                nextUpdate = 0;
                if (!Enabled) return;
            }
            if (Environment.TickCount64 < nextUpdate) return;
            nextUpdate = Environment.TickCount64 + 100;
            // Resolve the game's actual enemy-party positions, not ObjectTable enumeration order.
            // Refresh all rows; missing members must never retain the previous player's icon.
            Array.Clear(jobIcons); Array.Clear(enemyLabels);
            var pronouns = PronounModule.Instance();
            if (pronouns == null) return;
            for (var row = 0; row < 5; row++)
            {
                var target = pronouns->ResolvePlaceholder($"<e{row + 1}>", 0, 0);
                if (target == null) continue;
                foreach (var obj in Plugin.ObjectTable)
                    if (obj.Address == (nint)target && obj is IPlayerCharacter player && player.ClassJob.RowId > 0)
                    {
                        jobIcons[row] = 62100 + player.ClassJob.RowId;
                        enemyLabels[row] = $"{player.Name.TextValue} / {player.ClassJob.Value.Name.ExtractText()}";
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Array.Clear(jobIcons); Array.Clear(enemyLabels);
            Report(ex);
        }
    }

    private void PrepareSlot(RaptureHotbarModule* module, HotbarSlot* slot, HotbarUIIntermediate* output)
    {
        renderHook!.Original(module, slot, output);
        try
        {
            if (!Enabled || owner != Plugin.ObjectTable.LocalPlayer?.GameObjectId || territory != Plugin.ClientState.TerritoryType ||
                slot == null || output == null || slot->CommandType != HotbarSlotType.Macro) return;
            var mappings = Plugin.Configuration.PvpEnemyMacros;
            for (var row = 0; row < Math.Min(mappings.Length, 5); row++)
                if (mappings[row] == slot->CommandId)
                {
                    if (jobIcons[row] != 0) output->IconId = jobIcons[row];
                    return;
                }
        }
        catch (Exception ex) { Report(ex); }
    }

    internal static string MacroLabel(int id)
    {
        if (!PvpMacroRules.IsValid(id)) return "未設定";
        var module = RaptureMacroModule.Instance();
        var macro = module == null ? null : module->GetMacro((uint)id >> 8, (uint)id & 255);
        var name = macro == null ? string.Empty : macro->Name.ToString();
        return $"{(id < 256 ? "個人用" : "共有")} {id & 255:D2}：{(string.IsNullOrWhiteSpace(name) ? "（名前なし）" : name)}";
    }

    private void Report(Exception ex)
    {
        if (Environment.TickCount64 < nextLog) return;
        nextLog = Environment.TickCount64 + 5000;
        Plugin.Log.Error(ex, "For PvP Battle update failed.");
    }

    public void Dispose()
    {
        renderHook?.Dispose(); renderHook = null;
        Array.Clear(jobIcons); Array.Clear(enemyLabels);
    }
}
