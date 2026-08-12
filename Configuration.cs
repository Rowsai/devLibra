using Dalamud.Configuration;
using System.Numerics;

namespace devLibra;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;

    public bool ShowBarrierAdjustedHp { get; set; }

    public bool PartySearchEnabled { get; set; } = true;

    /// <summary>Replacement name used for the local character while solo.</summary>
    public string PartySearchDisplayName { get; set; } = string.Empty;

    /// <summary>Whether a custom color is used for the local character's name while solo.</summary>
    public bool PartySearchUseCustomNameColor { get; set; }

    /// <summary>Custom RGBA color used for the local character's name while solo.</summary>
    public Vector4 PartySearchNameColor { get; set; } = Vector4.One;
}
