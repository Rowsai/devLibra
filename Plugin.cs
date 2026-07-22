using System;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using devLibra.Windows;

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
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    internal static Configuration Configuration { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("devLibra");
    private readonly MainWindow mainWindow;
    private readonly PartyListBarrierHpDisplay partyListBarrierHpDisplay;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        this.mainWindow = new MainWindow();
        this.partyListBarrierHpDisplay = new PartyListBarrierHpDisplay();

        this.windowSystem.AddWindow(this.mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open devLibra window."
        });

        PluginInterface.UiBuilder.Draw += this.DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenUi;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenUi;
        Framework.Update += this.partyListBarrierHpDisplay.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenUi;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenUi;
        Framework.Update -= this.partyListBarrierHpDisplay.OnFrameworkUpdate;

        this.partyListBarrierHpDisplay.Dispose();

        CommandManager.RemoveHandler(CommandName);

        this.windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string args)
    {
        this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
    }

    private void DrawUi()
    {
        this.windowSystem.Draw();
    }

    private void OpenUi()
    {
        this.mainWindow.IsOpen = true;
    }

    internal static void SaveConfiguration()
    {
        PluginInterface.SavePluginConfig(Configuration);
    }
}
