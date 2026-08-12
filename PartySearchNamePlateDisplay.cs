using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace devLibra;

/// <summary>
/// Applies the party-search indicator and locally configured display names to
/// player nameplates. The nameplate service owns the backing UI data, so no
/// game-object memory is modified and changes are local to this client.
/// </summary>
internal sealed class PartySearchNamePlateDisplay : IDisposable
{
    private static readonly SeString PartySearchIcon = new(new IconPayload(BitmapFontIcon.LookingForParty));

    public PartySearchNamePlateDisplay()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate += this.OnNamePlateUpdate;
    }

    public void Dispose()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate -= this.OnNamePlateUpdate;
        Plugin.NamePlateGui.RequestRedraw();
    }

    private void OnNamePlateUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!Plugin.Configuration.PartySearchEnabled)
            return;

        try
        {
            foreach (var handler in handlers)
            {
                var player = handler.PlayerCharacter;
                if (player == null || this.IsPartyMember(handler, player))
                    continue;

                // StatusPrefix is rendered by the game directly to the left of
                // the name, after the job icon. BitmapFontIcon.LookingForParty
                // is the gold party-search symbol shown in the reference image.
                handler.StatusPrefix = PartySearchIcon;

                var originalName = player.Name.TextValue;
                if (!Plugin.Configuration.PartySearchPlayerNames.TryGetValue(originalName, out var replacementName)
                    || string.IsNullOrWhiteSpace(replacementName))
                    continue;

                handler.Name = replacementName;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Failed to update PartySearch nameplates.");
        }
    }

    private bool IsPartyMember(INamePlateUpdateHandler handler, IPlayerCharacter player)
    {
        // MainGroup has one member when the local player is solo. Treat that
        // case as "not in a party" so PartySearch still applies to the player.
        if (Plugin.PartyList.Length > 1
            && Plugin.PartyList.Any(member => member.EntityId == player.EntityId))
            return true;

        // The game includes party state in the standard nameplate status
        // prefix, so this also excludes visible players who are in a party
        // other than the local player's current party.
        return handler.StatusPrefix.Payloads
            .OfType<IconPayload>()
            .Any(payload => payload.Icon is BitmapFontIcon.PartyLeader
                or BitmapFontIcon.PartyMember
                or BitmapFontIcon.CrossWorldPartyLeader
                or BitmapFontIcon.CrossWorldPartyMember);
    }
}
