using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using devLibra.Windows;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

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

    internal static Configuration Configuration { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("devLibra");
    private readonly MainWindow mainWindow;
    private readonly PartyListBarrierHpDisplay partyListBarrierHpDisplay;
    private readonly PartySearchNamePlateDisplay partySearchNamePlateDisplay;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        this.mainWindow = new MainWindow();
        this.partyListBarrierHpDisplay = new PartyListBarrierHpDisplay();
        this.partySearchNamePlateDisplay = new PartySearchNamePlateDisplay();
        instance = this;

        this.windowSystem.AddWindow(this.mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open devLibra window."
        });

        PluginInterface.UiBuilder.Draw += this.DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenUi;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenUi;
        AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_PartyList", this.partyListBarrierHpDisplay.OnPartyListPostUpdate);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_PartyList", this.partyListBarrierHpDisplay.OnPartyListPreFinalize);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenUi;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenUi;
        AddonLifecycle.UnregisterListener(
            this.partyListBarrierHpDisplay.OnPartyListPostUpdate,
            this.partyListBarrierHpDisplay.OnPartyListPreFinalize);

        this.partyListBarrierHpDisplay.Dispose();
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
        this.DrawPartySearchTargetLines();
        this.windowSystem.Draw();
    }

    private void DrawPartySearchTargetLines()
    {
        if (!Configuration.PartySearchEnabled
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

    internal static IReadOnlyList<BarrierHpDebugInfo> GetBarrierHpDebugInfo()
        => instance?.partyListBarrierHpDisplay.GetDebugInfo()
            ?? Array.Empty<BarrierHpDebugInfo>();

    internal static IReadOnlyList<NearbyNameplatePlayerInfo> GetPartySearchNearbyPlayers()
        => instance?.partySearchNamePlateDisplay.GetNearbyPlayers()
            ?? Array.Empty<NearbyNameplatePlayerInfo>();

    internal static void RequestPartySearchNamePlateRedraw()
        => NamePlateGui.RequestRedraw();

    internal static unsafe void InviteToParty(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)
            || playerName.IndexOfAny(['\r', '\n']) >= 0)
            return;

        var uiModule = UIModule.Instance();
        if (uiModule == null)
            return;

        using var command = new Utf8String($"/invite {playerName}");
        uiModule->ProcessChatBoxEntry(&command);
    }

    private static Plugin? instance;
}
