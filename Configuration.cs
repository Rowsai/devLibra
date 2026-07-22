using Dalamud.Configuration;

namespace devLibra;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool ShowBarrierAdjustedHp { get; set; }
}
