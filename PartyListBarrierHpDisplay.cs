using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace devLibra;

/// <summary>
/// Replaces the HP number in the game's party list with current HP plus the
/// shield amount already calculated by the game's party-list data array.
/// </summary>
internal unsafe sealed class PartyListBarrierHpDisplay : IDisposable
{
    private static readonly ByteColor BarrierHpTextColor = new()
    {
        R = 88,
        G = 220,
        B = 120,
        A = 255
    };

    private readonly Dictionary<nint, ByteColor> defaultTextColors = new();

    /// <summary>
    /// Runs after the native party-list addon has updated its HP text.  Updating
    /// here prevents the game and the plugin from racing to write the same node.
    /// </summary>
    public void OnPartyListPostUpdate(AddonEvent eventType, AddonArgs args)
    {
        try
        {
            // Keep the game's known party-list lookup and its HP component.  The
            // gauge-bar child does not consistently expose the visible HP text.
            this.UpdatePartyList();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Failed to update party-list barrier HP display.");
        }
    }

    public void Dispose()
    {
        try
        {
            this.RestorePartyListHp();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Failed to restore party-list HP display.");
        }
    }

    /// <summary>
    /// The addon is about to be destroyed, so cached text-node addresses must
    /// not be used afterwards.
    /// </summary>
    public void OnPartyListPreFinalize(AddonEvent eventType, AddonArgs args)
        => this.defaultTextColors.Clear();

    private void UpdatePartyList()
    {
        var partyList = Plugin.GameGui.GetAddonByName<AddonPartyList>("_PartyList");
        var partyListData = PartyListNumberArray.Instance();

        if (partyList == null || partyListData == null)
            return;

        for (var index = 0; index < 8; index++)
        {
            var member = partyList->PartyMembers[index];
            var memberData = partyListData->PartyMembers[index];

            if (member.HPGaugeComponent == null || memberData.MaxHealth <= 0)
                continue;

            var shieldHp = CalculateShieldHp(memberData.MaxHealth, memberData.ShieldsPercentage);
            var hasShield = Plugin.Configuration.ShowBarrierAdjustedHp && shieldHp > 0;
            var displayHp = hasShield
                ? SaturatingAdd(memberData.CurrentHealth, shieldHp)
                : memberData.CurrentHealth;

            this.SetHpText(member.HPGaugeComponent, displayHp, hasShield);
        }
    }

    private void RestorePartyListHp()
    {
        foreach (var (address, color) in this.defaultTextColors)
        {
            var textNode = (AtkTextNode)address;
            if (textNode != null)
                textNode->TextColor = color;
        }

        this.defaultTextColors.Clear();
    }

    private void SetHpText(AtkComponentBase* hpGaugeComponent, int value, bool hasShield)
    {
        // The party-list component owns the displayed HP text.  Applying the
        // same value to its text nodes preserves the behaviour from v0.0.0.4.
        // This method runs in PostUpdate, after the game has completed its own
        // update, which avoids the original frame-timing race.
        foreach (var node in hpGaugeComponent->UldManager.Nodes)
        {
            if (node.Value == null || node.Value->Type != NodeType.Text)
                continue;

            var textNode = (AtkTextNode*)node.Value;
            var textNodeAddress = (nint)textNode;
            if (!this.defaultTextColors.ContainsKey(textNodeAddress))
                this.defaultTextColors[textNodeAddress] = textNode->TextColor;
            textNode->SetNumber(value, showCommaDelimiters: true);
            textNode->TextColor = hasShield
                ? BarrierHpTextColor
                : this.defaultTextColors[textNodeAddress];
        }
    }

    internal static int CalculateShieldHp(int maxHp, int shieldPercentage)
    {
        if (maxHp <= 0 || shieldPercentage <= 0)
            return 0;

        return (int)Math.Min(int.MaxValue, (long)maxHp * shieldPercentage / 100);
    }

    private static int SaturatingAdd(int currentHp, int shieldHp)
    {
        return (int)Math.Clamp((long)currentHp + shieldHp, 0, int.MaxValue);
    }
}
