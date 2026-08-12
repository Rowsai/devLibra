using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace devLibra;

/// <summary>
/// Applies the content-participation indicator and locally configured text to
/// nearby solo player nameplates while the local player is out of combat.
/// </summary>
internal sealed class PartySearchNamePlateDisplay : IDisposable
{
    // LookingForParty is supported by the nameplate bitmap font. The
    // WaitingForDutyFinder glyph is not available there and renders as '?'.
    private static readonly SeString ContentParticipationIcon = new(new IconPayload(BitmapFontIcon.LookingForParty));
    private readonly Dictionary<ulong, bool> contentParticipationByGameObjectId = [];
    private bool? wasActive;
    private long lastRedrawRequestAt;

    public PartySearchNamePlateDisplay()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate += this.OnNamePlateUpdate;
        Plugin.Framework.Update += this.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate -= this.OnNamePlateUpdate;
        Plugin.Framework.Update -= this.OnFrameworkUpdate;
        Plugin.NamePlateGui.RequestRedraw();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var isActive = this.IsFeatureActive();

        if (isActive)
        {
            // Party membership icons are supplied by the game with the
            // nameplate data. Refresh them so a party-state change is applied
            // without waiting for an unrelated UI update.
            var now = Environment.TickCount64;
            if (now - this.lastRedrawRequestAt >= 1000)
            {
                this.lastRedrawRequestAt = now;
                Plugin.NamePlateGui.RequestRedraw();
            }
        }

        if (this.wasActive == isActive)
            return;

        this.wasActive = isActive;
        Plugin.NamePlateGui.RequestRedraw();
    }

    private void OnNamePlateUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!this.IsFeatureActive())
            return;

        try
        {
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            if (localPlayer == null)
                return;

            foreach (var handler in handlers)
            {
                var player = handler.PlayerCharacter;
                if (player == null)
                    continue;

                // Job icons can be present in StatusPrefix when the character
                // configuration enables them. Check only the explicit content
                // participation icon value, never the icon position or count.
                var isContentParticipant = HasContentParticipationIcon(handler.StatusPrefix);
                this.contentParticipationByGameObjectId[player.GameObjectId] = isContentParticipant;

                if (player.GameObjectId == localPlayer.GameObjectId
                    || player.CurrentDistance > 100
                    || !isContentParticipant)
                    continue;

                // StatusPrefix is rendered directly to the left of the name,
                // after the job icon.
                handler.StatusPrefix = ContentParticipationIcon;

                if (!string.IsNullOrWhiteSpace(Plugin.Configuration.PartySearchDisplayName))
                    handler.Name = Plugin.Configuration.PartySearchDisplayName;

                if (Plugin.Configuration.PartySearchUseCustomNameColor)
                    handler.TextColor = ToGameColor(Plugin.Configuration.PartySearchNameColor);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Failed to update PartySearch nameplates.");
        }
    }

    private bool IsFeatureActive()
        => Plugin.Configuration.PartySearchEnabled
           && Plugin.ObjectTable.LocalPlayer != null
           && !Plugin.Condition[ConditionFlag.InCombat];

    internal IReadOnlyList<NearbyNameplatePlayerInfo> GetNearbyPlayers()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null)
            return Array.Empty<NearbyNameplatePlayerInfo>();

        return Plugin.ObjectTable
            .OfType<IPlayerCharacter>()
            .Where(player => player.GameObjectId != localPlayer.GameObjectId)
            .Where(player => player.CurrentDistance <= 100)
            .Where(player => !string.IsNullOrWhiteSpace(player.Name.TextValue))
            .OrderBy(player => player.CurrentDistance)
            .ThenBy(player => player.Name.TextValue, StringComparer.OrdinalIgnoreCase)
            .Select(player =>
            {
                var hasNameplateStatus = this.contentParticipationByGameObjectId.TryGetValue(
                    player.GameObjectId,
                    out var isContentParticipant);
                return new NearbyNameplatePlayerInfo(
                    player,
                    hasNameplateStatus,
                    isContentParticipant);
            })
            .ToArray();
    }

    private static bool HasContentParticipationIcon(SeString statusPrefix)
        => statusPrefix.Payloads
            .OfType<IconPayload>()
            .Any(payload => payload.Icon == BitmapFontIcon.LookingForParty);

    private static uint ToGameColor(Vector4 color)
    {
        static uint ToByte(float value) => (uint)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

        // ImGui/Dalamud's packed color format is ABGR (the native byte order
        // used by the nameplate text-color field).
        return (ToByte(color.W) << 24)
             | (ToByte(color.Z) << 16)
             | (ToByte(color.Y) << 8)
             | ToByte(color.X);
    }
}

internal sealed record NearbyNameplatePlayerInfo(
    IPlayerCharacter Player,
    bool HasNameplateStatus,
    bool IsContentParticipant);
