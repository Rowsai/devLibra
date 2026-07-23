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
            var partyList = (AddonPartyList*)args.Addon.Address;
            if (partyList == null || !partyList->AtkUnitBase.IsVisible)
                return;

            this.UpdatePartyList(partyList);
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

    private void UpdatePartyList(AddonPartyList* partyList)
    {
        var partyListData = PartyListNumberArray.Instance();

        if (partyListData == null)
            return;

        for (var index = 0; index < partyList->PartyMembers.Length; index++)
        {
            var member = partyList->PartyMembers[index];
            var memberData = partyListData->PartyMembers[index];

            var hpTextNode = GetHpTextNode(member.HPGaugeBar);
            if (hpTextNode == null || memberData.MaxHealth <= 0)
                continue;

            var shieldHp = CalculateShieldHp(memberData.MaxHealth, memberData.ShieldsPercentage);
            if (!Plugin.Configuration.ShowBarrierAdjustedHp || shieldHp <= 0)
                continue;

            this.SetHpText(hpTextNode, SaturatingAdd(memberData.CurrentHealth, shieldHp));
        }
    }

    private void RestorePartyListHp()
    {
        foreach (var (address, color) in this.defaultTextColors)
        {
            var textNode = (AtkTextNode*)address;
            if (textNode != null)
                textNode->TextColor = color;
        }

        this.defaultTextColors.Clear();
    }

    private void SetHpText(AtkTextNode* textNode, int value)
    {
        var textNodeAddress = (nint)textNode;

        if (!this.defaultTextColors.ContainsKey(textNodeAddress))
            this.defaultTextColors[textNodeAddress] = textNode->TextColor;

        textNode->SetNumber(value, showCommaDelimiters: true);
        textNode->TextColor = BarrierHpTextColor;
    }

    private static AtkTextNode* GetHpTextNode(AtkComponentGaugeBar* hpGauge)
    {
        if (hpGauge == null)
            return null;

        var uldManager = &hpGauge->AtkComponentBase.UldManager;
        for (var nodeIndex = 0; nodeIndex < uldManager->NodeListCount; nodeIndex++)
        {
            var node = uldManager->NodeList[nodeIndex];
            if (node != null && node->Type == NodeType.Text)
                return (AtkTextNode*)node;
        }

        return null;
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
