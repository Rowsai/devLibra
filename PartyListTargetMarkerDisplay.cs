using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace devLibra;

/// <summary>Displays assigned target markers in the native party-list job icon slot.</summary>
internal sealed unsafe class PartyListTargetMarkerDisplay : IDisposable
{
    private nint trackedAddon;
    private readonly uint[] originalIcons = new uint[8];
    private readonly uint[] appliedIcons = new uint[8];
    private readonly nint[] trackedNodes = new nint[8];

    // MarkingController order: attack 1–5, bind 1–3, ignore 1–2,
    // square/circle/cross/triangle, attack 6–8.
    private static readonly uint[] MarkerIcons =
    [61201, 61202, 61203, 61204, 61205, 61211, 61212, 61213,
     61221, 61222, 61231, 61232, 61233, 61234, 61206, 61207, 61208];

    public void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonPartyList*)args.Addon.Address;
        if (addon == null) return;
        if (trackedAddon != (nint)addon)
        {
            Forget();
            trackedAddon = (nint)addon;
        }
        if (!Plugin.Configuration.ChangePartyIconsEnabled)
        {
            Restore(addon);
            return;
        }

        Span<uint> desired = stackalloc uint[8];
        desired.Clear();
        var hud = AgentHUD.Instance();
        var marking = MarkingController.Instance();
        if (hud != null && marking != null)
        {
            var count = Math.Clamp((int)hud->PartyMemberCount, 0, hud->PartyMembers.Length);
            for (var i = 0; i < count; i++)
            {
                var member = hud->PartyMembers[i];
                // HUD member array order is not necessarily the visible party order.
                if (member.Index >= 8 || member.Index >= addon->MemberCount ||
                    member.EntityId is 0 or 0xE0000000) continue;
                for (var marker = 0; marker < MarkerIcons.Length; marker++)
                {
                    var target = marking->Markers[marker];
                    if (target.Type == 0 && target.ObjectId == member.EntityId)
                    {
                        desired[member.Index] = MarkerIcons[marker];
                        break;
                    }
                }
            }
        }

        for (var row = 0; row < 8; row++)
        {
            var node = addon->PartyMembers[row].ClassJobIcon;
            if (trackedNodes[row] != (nint)node)
            {
                originalIcons[row] = appliedIcons[row] = 0;
                trackedNodes[row] = (nint)node;
            }
            if (node == null) continue;
            var cached = addon->PartyClassJobIconId[row];
            // The game updates this cache when jobs, members or UI settings change.
            if (appliedIcons[row] == 0 || cached != appliedIcons[row])
                originalIcons[row] = cached;
            var icon = desired[row] != 0 ? desired[row] : originalIcons[row];
            if (icon != 0 && cached != icon)
            {
                node->LoadIconTexture(icon, 0);
                addon->PartyClassJobIconId[row] = icon;
            }
            appliedIcons[row] = desired[row];
        }
    }

    private void Restore(AddonPartyList* addon)
    {
        for (var row = 0; row < 8; row++)
        {
            var node = addon->PartyMembers[row].ClassJobIcon;
            if (appliedIcons[row] != 0 && originalIcons[row] != 0 &&
                node != null && (nint)node == trackedNodes[row] &&
                addon->PartyClassJobIconId[row] == appliedIcons[row])
            {
                node->LoadIconTexture(originalIcons[row], 0);
                addon->PartyClassJobIconId[row] = originalIcons[row];
            }
            appliedIcons[row] = 0;
        }
    }

    private void RestoreCurrent()
    {
        var addon = (AddonPartyList*)Plugin.GameGui.GetAddonByName("_PartyList").Address;
        if (addon != null && (nint)addon == trackedAddon) Restore(addon);
    }

    public void OnFinalize(AddonEvent type, AddonArgs args)
    {
        // Do not retain pointers across addon destruction, logout or HUD recreation.
        if (args.Addon.Address == trackedAddon) Forget();
    }

    private void Forget()
    {
        trackedAddon = 0;
        Array.Clear(originalIcons);
        Array.Clear(appliedIcons);
        Array.Clear(trackedNodes);
    }

    public void Dispose()
    {
        RestoreCurrent();
        Forget();
    }
}
