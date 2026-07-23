using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Interface.Windowing;

namespace devLibra.Windows;

public sealed class ScholarAetherflowOverlay : Window
{
    private const uint ScholarClassJobId = 28;

    private bool applySavedPosition = true;

    public ScholarAetherflowOverlay()
        : base(
            "Scholar Aetherflow###devLibraScholarAetherflowOverlay",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoCollapse)
    {
        this.RespectCloseHotkey = false;
        this.DisableWindowSounds = true;
    }

    public void UpdateVisibility()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        this.IsOpen = Plugin.Configuration.ShowScholarAetherflowOverlay
            && localPlayer?.ClassJob.RowId == ScholarClassJobId;
    }

    public void ResetPosition()
    {
        this.applySavedPosition = true;
    }

    public override void PreDraw()
    {
        var configuration = Plugin.Configuration;
        this.Flags = ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoCollapse;

        if (configuration.ScholarAetherflowOverlayLocked)
            this.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar;

        var barHeight = this.GetBarHeight();
        this.Size = new Vector2(
            configuration.ScholarAetherflowOverlayWidth,
            barHeight + 40);
        this.SizeCondition = ImGuiCond.Always;

        if (!this.applySavedPosition)
            return;

        this.Position = configuration.ScholarAetherflowOverlayPosition;
        this.PositionCondition = ImGuiCond.Always;
        this.applySavedPosition = false;
    }

    public override void Draw()
    {
        var aetherflow = Plugin.JobGauges.Get<SCHGauge>().Aetherflow;

        ImGui.TextUnformatted("AETHERFLOW");
        ImGui.SameLine();
        ImGui.TextDisabled("SCH");

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.28f, 0.68f, 0.96f, 1.0f));
        ImGui.ProgressBar(aetherflow / 3.0f, new Vector2(-1, this.GetBarHeight()), $"{aetherflow} / 3");
        ImGui.PopStyleColor();

        if (!Plugin.Configuration.ScholarAetherflowOverlayLocked)
            this.SaveMovedPosition();
    }

    private void SaveMovedPosition()
    {
        var position = ImGui.GetWindowPos();
        if (Vector2.DistanceSquared(position, Plugin.Configuration.ScholarAetherflowOverlayPosition) < 0.01f)
            return;

        Plugin.Configuration.ScholarAetherflowOverlayPosition = position;
        Plugin.SaveConfiguration();
    }

    private float GetBarHeight()
    {
        return Math.Clamp(
            Plugin.Configuration.ScholarAetherflowOverlayWidth * 0.1f,
            18.0f,
            64.0f);
    }
}
