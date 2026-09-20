using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using devLibra.Windows;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace devLibra;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "devLibra";

    private const string CommandName = "/devlibra";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static ICondition Condition { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IPartyList PartyList { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService]
    internal static INamePlateGui NamePlateGui { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    internal static Configuration Configuration { get; private set; } = null!;

    internal static bool InPvpContent => ClientState.IsPvPExcludingDen;
    internal static bool PvpAllowsBarrierHp => !InPvpContent || Configuration.PvpAllowBarrierHp;
    internal static bool PvpAllowsPartySearch => !InPvpContent || Configuration.PvpAllowPartySearch;
    internal static bool PvpAllowsChangePartyIcons => !InPvpContent || Configuration.PvpAllowChangePartyIcons;
    internal static bool PvpAllowsViewDotIcons => !InPvpContent || Configuration.PvpAllowViewDotIcons;

    private readonly WindowSystem windowSystem = new("devLibra");
    private readonly MainWindow mainWindow;
    private readonly PartyListSorter partyListSorter;
    private readonly EnemyDotNameplateDisplay enemyDotNameplateDisplay;
    private readonly PartyListTargetMarkerDisplay partyListTargetMarkerDisplay;
    private readonly PartyListBarrierHpDisplay partyListBarrierHpDisplay;
    private readonly BarrierHpIpc barrierHpIpc;
    private readonly PartySearchNamePlateDisplay partySearchNamePlateDisplay;
    private readonly ConcurrentQueue<PartyInviteRequest> partyInviteRequests = [];
    private readonly ConcurrentDictionary<ulong, string> partyInviteResults = [];

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.OriginalTargetMarkerOrder = TargetMarkerSortOrder.Normalize(Configuration.OriginalTargetMarkerOrder);
        this.partyListSorter = new PartyListSorter();
        this.enemyDotNameplateDisplay = new EnemyDotNameplateDisplay();

        this.mainWindow = new MainWindow();
        this.partyListTargetMarkerDisplay = new PartyListTargetMarkerDisplay();
        this.barrierHpIpc = new BarrierHpIpc();
        this.partyListBarrierHpDisplay = new PartyListBarrierHpDisplay(this.barrierHpIpc);
        this.partySearchNamePlateDisplay = new PartySearchNamePlateDisplay();
        instance = this;

        this.windowSystem.AddWindow(this.mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "devLibraの設定画面を開きます。"
        });

        PluginInterface.UiBuilder.Draw += this.DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenUi;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenUi;
        AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_PartyList", this.partyListBarrierHpDisplay.OnPartyListPostUpdate);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_PartyList", this.partyListBarrierHpDisplay.OnPartyListPreFinalize);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_PartyList", this.partyListTargetMarkerDisplay.OnPreDraw);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_PartyList", this.partyListTargetMarkerDisplay.OnFinalize);
        Framework.Update += this.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenUi;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenUi;
        AddonLifecycle.UnregisterListener(
            this.partyListBarrierHpDisplay.OnPartyListPostUpdate,
            this.partyListBarrierHpDisplay.OnPartyListPreFinalize);
        AddonLifecycle.UnregisterListener(
            this.partyListTargetMarkerDisplay.OnPreDraw,
            this.partyListTargetMarkerDisplay.OnFinalize);
        Framework.Update -= this.OnFrameworkUpdate;

        this.partyListTargetMarkerDisplay.Dispose();
        this.partyListSorter.Dispose();
        this.enemyDotNameplateDisplay.Dispose();
        this.partyListBarrierHpDisplay.Dispose();
        this.barrierHpIpc.Dispose();
        this.partySearchNamePlateDisplay.Dispose();
        instance = null;

        CommandManager.RemoveHandler(CommandName);

        this.windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string args)
    {
        this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
    }

    private void DrawUi()
    {
        this.enemyDotNameplateDisplay.Draw();
        this.DrawPartySearchTargetLines();
        using var theme = new DarkBlueTheme();
        this.windowSystem.Draw();
    }

    private void DrawPartySearchTargetLines()
    {
        if (!PvpAllowsPartySearch || !Configuration.PartySearchEnabled
            || !Configuration.PartySearchDrawTargetLines
            || Condition[ConditionFlag.InCombat])
            return;

        var localPlayer = ObjectTable.LocalPlayer;
        if (localPlayer == null)
            return;

        if (!GameGui.WorldToScreen(localPlayer.Position + Vector3.UnitY, out var sourcePosition))
            sourcePosition = ImGui.GetMainViewport().GetCenter();

        var drawList = ImGui.GetForegroundDrawList();
        var color = ImGui.GetColorU32(Configuration.PartySearchTargetLineColor);
        var thickness = Math.Clamp(Configuration.PartySearchTargetLineThickness, 1f, 10f);

        foreach (var player in this.partySearchNamePlateDisplay.GetEligiblePlayers())
        {
            if (!GameGui.WorldToScreen(player.Position + Vector3.UnitY, out var targetPosition))
                continue;

            drawList.AddLine(sourcePosition, targetPosition, color, thickness);
        }
    }

    private void OpenUi()
    {
        this.mainWindow.IsOpen = true;
    }

    internal static void SaveConfiguration()
    {
        PluginInterface.SavePluginConfig(Configuration);
    }

    internal static bool DotCommandRegistered => instance?.enemyDotNameplateDisplay.CommandRegistered ?? false;
    internal static bool SortCommandRegistered => instance?.partyListSorter.CommandRegistered ?? false;
    internal static string? DotCatalogError => instance?.enemyDotNameplateDisplay.CatalogError;

    internal static IReadOnlyList<BarrierHpDebugInfo> GetBarrierHpDebugInfo()
        => instance?.partyListBarrierHpDisplay.GetDebugInfo()
            ?? Array.Empty<BarrierHpDebugInfo>();

    internal static IReadOnlyList<NearbyNameplatePlayerInfo> GetPartySearchNearbyPlayers()
        => instance?.partySearchNamePlateDisplay.GetNearbyPlayers()
            ?? Array.Empty<NearbyNameplatePlayerInfo>();

    internal static void RequestPartySearchNamePlateRedraw()
        => NamePlateGui.RequestRedraw();

    internal static unsafe bool CanInviteToParty(IPlayerCharacter player)
    {
        if (!PvpAllowsPartySearch) return false;
        if (ObjectTable.LocalPlayer == null || string.IsNullOrWhiteSpace(player.Name.TextValue))
            return false;

        var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
        return character != null && character->ContentId != 0;
    }

    // The native party-invite functions must be called from Framework.Update,
    // rather than while ImGui is being drawn.  Queue button clicks here and
    // carry out the request on the next game framework update instead.
    private void OnFrameworkUpdate(IFramework framework)
    {
        this.partyListSorter.Update();
        while (this.partyInviteRequests.TryDequeue(out var request))
        {
            try
            {
                this.partyInviteResults[request.GameObjectId] = this.SendPartyInvite(request);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send a PartySearch party invitation.");
                this.partyInviteResults[request.GameObjectId] =
                    $"{request.PlayerName}への招待に失敗しました。";
            }
        }
    }

    internal static void InviteToParty(IPlayerCharacter player)
    {
        if (instance == null)
            return;

        instance.partyInviteResults[player.GameObjectId] =
            $"{player.Name.TextValue}へ招待を送信しています…";
        instance.partyInviteRequests.Enqueue(new PartyInviteRequest(
            player.GameObjectId,
            player.Name.TextValue));
    }

    internal static string? GetPartyInviteResult(ulong gameObjectId)
        => instance != null && instance.partyInviteResults.TryGetValue(gameObjectId, out var result)
            ? result
            : null;

    private unsafe string SendPartyInvite(PartyInviteRequest request)
    {
        var localPlayer = ObjectTable.LocalPlayer;
        var player = ObjectTable
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(candidate => candidate.GameObjectId == request.GameObjectId);
        if (localPlayer == null || player == null || !CanInviteToParty(player))
            return $"{request.PlayerName}を招待できません：対象が見つかりません。";

        var targetCharacter = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
        var localCharacter = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)localPlayer.Address;
        var partyInvite = InfoProxyPartyInvite.Instance();
        if (targetCharacter == null || localCharacter == null || partyInvite == null)
            return $"{request.PlayerName}を招待できません：パーティ情報を取得できません。";

        // These targets are marked as being in content.  The client has a
        // dedicated in-instance invite route for them; ordinary same/cross-
        // world invite routes do not open an invite for a player in the same
        // content instance.
        if (partyInvite->InviteToPartyInInstanceByContentId(targetCharacter->ContentId))
            return $"{player.Name.TextValue}へ招待を送信しました。";

        // Fall back for a target that has left the shared instance between the
        // list refresh and this framework update.
        if (targetCharacter->CurrentWorld != localCharacter->CurrentWorld)
        {
            return partyInvite->InviteToPartyContentId(targetCharacter->ContentId, targetCharacter->CurrentWorld)
                ? $"{player.Name.TextValue}へ招待を送信しました。"
                : $"{player.Name.TextValue}への招待に失敗しました。";
        }

        return partyInvite->InviteToParty(
                targetCharacter->ContentId,
                player.Name.TextValue,
                targetCharacter->HomeWorld)
            ? $"{player.Name.TextValue}へ招待を送信しました。"
            : $"{player.Name.TextValue}への招待に失敗しました。";
    }

    private static Plugin? instance;

    private sealed record PartyInviteRequest(ulong GameObjectId, string PlayerName);
}


