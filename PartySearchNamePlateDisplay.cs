using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace devLibra;

/// <summary>
/// Applies the content-participation indicator and locally configured text to
/// the local player's nameplate only while that player is solo.
/// </summary>
internal sealed class PartySearchNamePlateDisplay : IDisposable
{
    private static readonly SeString ContentParticipationIcon = new(new IconPayload(BitmapFontIcon.WaitingForDutyFinder));
    private bool? wasSolo;

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
        var isSolo = Plugin.ObjectTable.LocalPlayer != null && Plugin.PartyList.Length <= 1;
        if (this.wasSolo == isSolo)
            return;

        this.wasSolo = isSolo;
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
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            if (localPlayer == null || Plugin.PartyList.Length > 1)
                return;

            foreach (var handler in handlers)
            {
                var player = handler.PlayerCharacter;
                if (player == null || player.GameObjectId != localPlayer.GameObjectId)
                    continue;

                // StatusPrefix is rendered directly to the left of the name,
                // after the job icon. WaitingForDutyFinder is the game's
                // "content participation" icon.
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
