using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Interface.Windowing;

namespace devLibra.Windows;

public sealed class ScholarAetherflowOverlay : Window
{
    private const uint ScholarClassJobId = 28;
    private const int FairyGaugeMaximum = 100;
    private const int SegmentCount = 10;

    private bool applySavedPosition = true;

    public ScholarAetherflowOverlay()
        : base(
            "Fairy Gauge###devLibraScholarAetherflowOverlay",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoBackground)
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
        this.Flags = ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoBackground;

        if (configuration.ScholarAetherflowOverlayLocked)
            this.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar;

        var overlayHeight = this.GetOverlayHeight();
        this.Size = new Vector2(
            configuration.ScholarAetherflowOverlayWidth,
            overlayHeight + (configuration.ScholarAetherflowOverlayLocked ? 0 : ImGui.GetFrameHeight()));
        this.SizeCondition = ImGuiCond.Always;

        if (!this.applySavedPosition)
        {
            this.PositionCondition = ImGuiCond.None;
            return;
        }

        this.Position = configuration.ScholarAetherflowOverlayPosition;
        this.PositionCondition = ImGuiCond.Always;
        this.applySavedPosition = false;
    }

    public override void Draw()
    {
        var fairyGauge = Math.Clamp(
            (int)Plugin.JobGauges.Get<SCHGauge>().FairyGauge,
            0,
            FairyGaugeMaximum);
        this.DrawFairyGauge(fairyGauge);

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

    private void DrawFairyGauge(int fairyGauge)
    {
        var width = Plugin.Configuration.ScholarAetherflowOverlayWidth;
        var height = this.GetOverlayHeight();
        var origin = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();
        var scale = width / 240.0f;
        var padding = 12.0f * scale;
        var panelEnd = origin + new Vector2(width, height);
        var headerHeight = 28.0f * scale;
        var barHeight = Math.Clamp(22.0f * scale, 18.0f, 44.0f);
        var barStart = origin + new Vector2(padding, headerHeight + padding);
        var barEnd = new Vector2(panelEnd.X - padding, barStart.Y + barHeight);
        var barWidth = barEnd.X - barStart.X;
        var segmentGap = Math.Clamp(3.0f * scale, 2.0f, 6.0f);
        var segmentWidth = (barWidth - segmentGap * (SegmentCount - 1)) / SegmentCount;
        var filledSegments = (int)MathF.Ceiling(fairyGauge / (float)(FairyGaugeMaximum / SegmentCount));

        drawList.AddRectFilled(origin, panelEnd, this.ToColor(0.035f, 0.08f, 0.14f, 0.94f), 10.0f * scale);
        drawList.AddRect(origin, panelEnd, this.ToColor(0.35f, 0.88f, 0.98f, 0.8f), 10.0f * scale, 0, 1.5f * scale);
        drawList.AddRectFilled(
            origin + new Vector2(1.5f * scale, 1.5f * scale),
            new Vector2(panelEnd.X - 1.5f * scale, origin.Y + 5.0f * scale),
            this.ToColor(0.32f, 0.86f, 0.98f, 0.9f),
            5.0f * scale);

        var titlePosition = origin + new Vector2(padding, 8.0f * scale);
        drawList.AddText(titlePosition, this.ToColor(0.75f, 0.95f, 1.0f, 1.0f), "FAIRY GAUGE");

        var valueText = $"{fairyGauge:D3}%";
        var valueSize = ImGui.CalcTextSize(valueText);
        drawList.AddText(
            new Vector2(panelEnd.X - padding - valueSize.X, titlePosition.Y),
            this.ToColor(0.65f, 0.95f, 1.0f, 1.0f),
            valueText);

        drawList.AddRectFilled(
            barStart - new Vector2(3.0f * scale),
            barEnd + new Vector2(3.0f * scale),
            this.ToColor(0.08f, 0.16f, 0.24f, 1.0f),
            6.0f * scale);

        for (var index = 0; index < SegmentCount; index++)
        {
            var segmentStart = new Vector2(barStart.X + index * (segmentWidth + segmentGap), barStart.Y);
            var segmentEnd = new Vector2(segmentStart.X + segmentWidth, barEnd.Y);
            var isFilled = index < filledSegments;

            if (isFilled)
            {
                drawList.AddRectFilled(
                    segmentStart - new Vector2(1.5f * scale),
                    segmentEnd + new Vector2(1.5f * scale),
                    this.ToColor(0.18f, 0.86f, 1.0f, 0.28f),
                    4.0f * scale);
            }

            drawList.AddRectFilled(
                segmentStart,
                segmentEnd,
                isFilled
                    ? this.ToColor(0.22f, 0.76f, 0.98f, 1.0f)
                    : this.ToColor(0.10f, 0.22f, 0.32f, 1.0f),
                3.0f * scale);
            drawList.AddRect(
                segmentStart,
                segmentEnd,
                this.ToColor(0.42f, 0.88f, 1.0f, isFilled ? 1.0f : 0.35f),
                3.0f * scale);
        }

        var markerPosition = new Vector2(padding, barEnd.Y + 8.0f * scale);
        drawList.AddText(markerPosition, this.ToColor(0.45f, 0.65f, 0.78f, 1.0f), "0");
        var maximumText = FairyGaugeMaximum.ToString();
        var maximumTextSize = ImGui.CalcTextSize(maximumText);
        drawList.AddText(
            new Vector2(panelEnd.X - padding - maximumTextSize.X, markerPosition.Y),
            this.ToColor(0.45f, 0.65f, 0.78f, 1.0f),
            maximumText);
    }

    private float GetOverlayHeight()
    {
        return Math.Clamp(
            Plugin.Configuration.ScholarAetherflowOverlayWidth * 0.35f,
            88.0f,
            150.0f);
    }

    private uint ToColor(float red, float green, float blue, float alpha)
    {
        return ImGui.GetColorU32(new Vector4(red, green, blue, alpha));
    }
}
