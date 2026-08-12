using Dalamud.Configuration;
using System.Numerics;

namespace devLibra;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 5;

    public bool ShowBarrierAdjustedHp { get; set; }

    public bool PartySearchEnabled { get; set; } = true;

    /// <summary>Replacement name used for the local character while solo.</summary>
    public string PartySearchDisplayName { get; set; } = string.Empty;

    /// <summary>Whether a custom color is used for the local character's name while solo.</summary>
    public bool PartySearchUseCustomNameColor { get; set; }

    /// <summary>Custom RGBA color used for the local character's name while solo.</summary>
    public Vector4 PartySearchNameColor { get; set; } = Vector4.One;

    /// <summary>Whether target lines are drawn from the local player to eligible players.</summary>
    public bool PartySearchDrawTargetLines { get; set; } = true;

    /// <summary>RGBA color used for eligible-player target lines.</summary>
    public Vector4 PartySearchTargetLineColor { get; set; } = new(1f, 0.82f, 0.1f, 0.9f);

    /// <summary>Screen-space thickness of eligible-player target lines.</summary>
    public float PartySearchTargetLineThickness { get; set; } = 2f;
}
