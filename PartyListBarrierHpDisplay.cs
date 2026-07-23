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
    private const int GalvanizeShieldPercent = 180;
    private const long RecentRecoveryWindowMilliseconds = 2_000;

    private static readonly ByteColor BarrierHpTextColor = new()
    {
        R = 88,
        G = 220,
        B = 120,
        A = 255
    };

    private readonly Dictionary<nint, ByteColor> defaultTextColors = new();
    private readonly Dictionary<int, BarrierHpMemberState> memberStates = new();
    private readonly HashSet<uint> galvanizeStatusIds = new() { 297 };
    private readonly List<BarrierHpDebugInfo> debugInfo = new();
    private bool galvanizeStatusIdsResolved;

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
    {
        this.defaultTextColors.Clear();
        this.memberStates.Clear();
        this.debugInfo.Clear();
    }

    internal IReadOnlyList<BarrierHpDebugInfo> GetDebugInfo()
        => this.debugInfo.ToArray();

    private void UpdatePartyList()
    {
        var partyList = Plugin.GameGui.GetAddonByName<AddonPartyList>("_PartyList");
        var partyListData = PartyListNumberArray.Instance();

        if (partyList == null || partyListData == null)
            return;

        this.TryResolveGalvanizeStatusIds();
        this.debugInfo.Clear();

        for (var index = 0; index < 8; index++)
        {
            var member = partyList->PartyMembers[index];
            var memberData = partyListData->PartyMembers[index];

            if (member.HPGaugeComponent == null || memberData.MaxHealth <= 0)
                continue;

            var shieldHp = this.CalculateShieldHp(index, member, memberData);
            var hasShield = Plugin.Configuration.ShowBarrierAdjustedHp && shieldHp > 0;
            var displayHp = hasShield
                ? SaturatingAdd(memberData.CurrentHealth, shieldHp)
                : memberData.CurrentHealth;

            this.SetHpText(member.HPGaugeComponent, displayHp, hasShield);
            this.debugInfo.Add(new BarrierHpDebugInfo(
                index + 1,
                memberData.CurrentHealth,
                memberData.MaxHealth,
                memberData.ShieldsPercentage,
                this.memberStates[index].LastObservedRecoveryHp,
                shieldHp,
                displayHp,
                this.memberStates[index].CalculationSource,
                member.HPGaugeBar == null ? 0 : member.HPGaugeBar->MaxValue,
                member.HPGaugeBar == null ? 0 : member.HPGaugeBar->Values[0].ValueInt,
                member.HPGaugeBar == null ? 0 : member.HPGaugeBar->Values[1].ValueInt));
        }
    }

    private int CalculateShieldHp(
        int index,
        AddonPartyList.PartyListMemberStruct member,
        PartyListNumberArray.PartyListMemberNumberArray memberData)
    {
        var now = Environment.TickCount64;
        var hasGalvanize = this.HasGalvanize(index);

        if (!this.memberStates.TryGetValue(index, out var state))
            state = new BarrierHpMemberState();

        // A party slot can be reused when its member changes.  Do not carry a
        // previous member's observed heal or shield into the new slot.
        if (state.ContentId != 0 && state.ContentId != memberData.ContentId)
            state = new BarrierHpMemberState();

        var observedRecovery = state.HasObservedData
            ? Math.Max(0, memberData.CurrentHealth - state.LastCurrentHp)
            : 0;
        if (observedRecovery > 0)
        {
            state.LastObservedRecoveryHp = observedRecovery;
            state.LastRecoveryAtMilliseconds = now;
        }

        var shieldIncreased = state.HasObservedData
            && memberData.ShieldsPercentage > state.LastShieldPercentage;
        if (shieldIncreased)
        {
            state.LastShieldIncreaseAtMilliseconds = now;
            state.ShieldPercentageAtLastIncrease = memberData.ShieldsPercentage;
        }

        var hasRecentObservedRecovery = state.LastObservedRecoveryHp > 0
            && now - state.LastRecoveryAtMilliseconds <= RecentRecoveryWindowMilliseconds;

        // The number array exposes a whole-percent shield value, which loses
        // the fractional part.  For Galvanize, retain the actual recovery seen
        // when the shield is applied and derive its 180% barrier from that
        // value.  For example, 23,021 recovery -> 41,437 barrier (truncated by
        // the game), rather than 22% of max HP.
        if (hasGalvanize
            && state.ExactShieldHp == 0
            && memberData.ShieldsPercentage > 0
            && memberData.ShieldsPercentage == state.ShieldPercentageAtLastIncrease
            && now - state.LastShieldIncreaseAtMilliseconds <= RecentRecoveryWindowMilliseconds
            && hasRecentObservedRecovery)
        {
            state.ExactShieldHp = CalculateShieldHpFromRecovery(
                state.LastObservedRecoveryHp,
                GalvanizeShieldPercent);
            state.ExactShieldPercentage = memberData.ShieldsPercentage;
            state.CalculationSource = "Galvanize (observed recovery x 180%)";
        }
        else if (memberData.ShieldsPercentage != state.ExactShieldPercentage)
        {
            // Once absorbed damage has changed the displayed shield percent,
            // no exact remaining value is available from the party list data.
            // Fall back to the game's current whole-percent representation.
            state.ExactShieldHp = 0;
            state.ExactShieldPercentage = 0;
        }

        var shieldHp = state.ExactShieldHp > 0
            ? state.ExactShieldHp
            : CalculateShieldHp(memberData.MaxHealth, memberData.ShieldsPercentage);

        if (state.ExactShieldHp <= 0)
            state.CalculationSource = "Party-list shield percentage";

        state.ContentId = memberData.ContentId;
        state.HasObservedData = true;
        state.LastCurrentHp = memberData.CurrentHealth;
        state.LastShieldPercentage = memberData.ShieldsPercentage;
        this.memberStates[index] = state;

        return shieldHp;
    }

    private bool HasGalvanize(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= Plugin.PartyList.Length)
            return false;

        var partyMember = Plugin.PartyList[partyIndex];
        if (partyMember == null)
            return false;

        foreach (var status in partyMember.Statuses)
        {
            if (this.galvanizeStatusIds.Contains(status.StatusId))
                return true;
        }

        return false;
    }

    private void TryResolveGalvanizeStatusIds()
    {
        if (this.galvanizeStatusIdsResolved)
            return;

        try
        {
            var statusSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            foreach (var status in statusSheet)
            {
                var name = status.Name.ExtractText();
                if (name is "Galvanize" or "鼓舞")
                    this.galvanizeStatusIds.Add(status.RowId);
            }

            this.galvanizeStatusIdsResolved = true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Failed to resolve the Galvanize status id.");
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

    internal static int CalculateShieldHpFromRecovery(int recoveryHp, int shieldPercent)
    {
        if (recoveryHp <= 0 || shieldPercent <= 0)
            return 0;

        return (int)Math.Min(int.MaxValue, (long)recoveryHp * shieldPercent / 100);
    }

    private static int SaturatingAdd(int currentHp, int shieldHp)
    {
        return (int)Math.Clamp((long)currentHp + shieldHp, 0, int.MaxValue);
    }
}

internal readonly record struct BarrierHpDebugInfo(
    int PartyIndex,
    int CurrentHp,
    int MaxHp,
    int ShieldPercentage,
    int ObservedRecoveryHp,
    int BarrierHp,
    int DisplayHp,
    string CalculationSource,
    int GaugeMaxValue,
    int GaugePrimaryValue,
    int GaugeSecondaryValue);

internal sealed class BarrierHpMemberState
{
    public int ContentId { get; set; }

    public bool HasObservedData { get; set; }

    public int LastCurrentHp { get; set; }

    public int LastShieldPercentage { get; set; }

    public int LastObservedRecoveryHp { get; set; }

    public long LastRecoveryAtMilliseconds { get; set; }

    public long LastShieldIncreaseAtMilliseconds { get; set; }

    public int ShieldPercentageAtLastIncrease { get; set; }

    public int ExactShieldHp { get; set; }

    public int ExactShieldPercentage { get; set; }

    public string CalculationSource { get; set; } = "Party-list shield percentage";
}
