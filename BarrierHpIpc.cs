using System;
using System.Collections.Generic;
using System.Text.Json;
using Dalamud.Plugin.Ipc;

namespace devLibra;

// Versioned wire contract uses JSON/primitives, so consumers do not reference this assembly.
internal sealed class BarrierHpIpc : IDisposable
{
    private readonly ICallGateProvider<int> version;
    private readonly ICallGateProvider<uint, string> snapshot;
    private readonly Dictionary<uint, BarrierHpSample> samples = new();
    public BarrierHpIpc()
    {
        version = Plugin.PluginInterface.GetIpcProvider<int>("devLibra.BarrierHP.Version");
        snapshot = Plugin.PluginInterface.GetIpcProvider<uint, string>("devLibra.BarrierHP.SnapshotV1");
        version.RegisterFunc(() => 1);
        snapshot.RegisterFunc(Read);
    }
    internal void BeginFrame() => samples.Clear();
    internal unsafe void Publish(uint entityId, int hp, int maximum, int percent, int barrier, string source)
    {
        if (entityId is 0 or 0xE0000000 || maximum <= 0) return;
        var replay = FFXIVClientStructs.FFXIV.Client.Game.ContentsReplayManager.Instance();
        var isReplay = replay != null && (replay->PlaybackControls & FFXIVClientStructs.FFXIV.Client.Game.ContentsReplayPlaybackControl.InPlayback) != 0;
        samples[entityId] = new(1, entityId, hp, maximum, percent, barrier, source, Environment.TickCount64, isReplay);
    }
    private string Read(uint entityId) => samples.TryGetValue(entityId, out var value) &&
        Environment.TickCount64 - value.SampledAtMilliseconds is >= 0 and <= 500 ? JsonSerializer.Serialize(value) : "";
    public void Dispose()
    {
        snapshot.UnregisterFunc(); version.UnregisterFunc(); samples.Clear();
    }
}
internal sealed record BarrierHpSample(int Version, uint EntityId, int CurrentHp, int MaxHp, int ShieldPercent,
    int BarrierHp, string CalculationSource, long SampledAtMilliseconds, bool IsReplay);
