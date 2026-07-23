using Dalamud.Configuration;
using System.Numerics;

namespace devLibra;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool ShowBarrierAdjustedHp { get; set; }

    public bool ShowScholarAetherflowOverlay { get; set; }

    public bool ScholarAetherflowOverlayLocked { get; set; } = true;

    public int ScholarAetherflowOverlayWidth { get; set; } = 240;

    public Vector2 ScholarAetherflowOverlayPosition { get; set; } = new(500, 500);
}
