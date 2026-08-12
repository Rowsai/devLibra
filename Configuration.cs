using Dalamud.Configuration;
using System.Collections.Generic;

namespace devLibra;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    public bool ShowBarrierAdjustedHp { get; set; }

    public bool PartySearchEnabled { get; set; } = true;

    /// <summary>
    /// Per-character replacement names, keyed by the character's displayed name.
    /// These names are rendered locally only and are never sent to the game server.
    /// </summary>
    public Dictionary<string, string> PartySearchPlayerNames { get; set; } = new();
}
