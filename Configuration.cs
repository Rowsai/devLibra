using Dalamud.Configuration;
using System.Numerics;

namespace devLibra;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 5;
    public System.Collections.Generic.List<SavedFieldMarkerSet> FieldMarkerSets { get; set; } = [];

    public bool PvpAllowBarrierHp { get; set; }
    public bool PvpAllowPartySearch { get; set; }
    public bool PvpAllowChangePartyIcons { get; set; }
    public bool PvpAllowViewDotIcons { get; set; }

    public bool ChangePartyIconsEnabled { get; set; } = true;

    public bool ViewDotIconsEnabled { get; set; }
    public bool TitlesSearchEnabled { get; set; }
    public bool EqualizationEnabled { get; set; }
    public System.Collections.Generic.HashSet<uint> EqualizationContents { get; set; } = [];
    public float EqualizationX { get; set; } = 400;
    public float EqualizationY { get; set; } = 300;
    public bool CheckReviveTargetEnabled { get; set; }
    public int EnemyActionIconSize { get; set; } = 32;
    public int EnemyActionTimerFont { get; set; }
    public bool EnemyActionTimerBold { get; set; } = true;
    public float EnemyActionTimerBoldThickness { get; set; } = 0.6f;
    public bool EnemyActionTimerOutlineEnabled { get; set; } = true;
    public Vector4 EnemyActionTimerOutlineColor { get; set; } = new(0, 0, 0, 1);
    public float EnemyActionTimerOutlineThickness { get; set; } = 1;
    public float EnemyActionsX { get; set; } = 500;
    public float EnemyActionsY { get; set; } = 300;
    public bool EnemyActionsEnabled { get; set; }
    public bool EnemyActionsTransparentBackground { get; set; }
    public System.Collections.Generic.Dictionary<uint, System.Collections.Generic.HashSet<uint>> EnemyActionSelections { get; set; } = [];
    public bool ForPvpBattleEnabled { get; set; }
    public bool PvpTargetWarningEnabled { get; set; }
    public int PvpWarningTargetThreshold { get; set; } = 3;
    public bool PvpWarningSoundEnabled { get; set; }
    public int PvpWarningSoundId { get; set; } = 1;
    public float PvpWarningSoundVolume { get; set; } = 100;
    public string PvpWarningText { get; set; } = "Multiple enemy targets detected!";
    public int PvpWarningFont { get; set; }
    public float PvpWarningFontSize { get; set; } = 28;
    public Vector4 PvpWarningTextColor { get; set; } = new(1, 0.35f, 0.3f, 1);
    public bool PvpWarningRainbowEnabled { get; set; }
    public bool PvpWarningOutlineEnabled { get; set; } = true;
    public Vector4 PvpWarningOutlineColor { get; set; } = new(0, 0, 0, 1);
    public float PvpWarningOutlineThickness { get; set; } = 1;
    public float PvpWarningX { get; set; } = 400;
    public float PvpWarningY { get; set; } = 200;
    public int[]? PvpEnemyMacroSlots { get; set; }
    public int[]? PvpAllyMacroSlots { get; set; }
    public int[] PvpEnemyMacros { get; set; } = [-1, -1, -1, -1, -1];
    public int[] PvpAllyMacros { get; set; } = [-1, -1, -1, -1, -1];

    public int DotIconSize { get; set; } = 32;

    public int DotTimerFontSize { get; set; } = 16;
    public Vector4 DotTimerTextColor { get; set; } = Vector4.One;
    public bool DotTimerOutlineEnabled { get; set; } = true;
    public Vector4 DotTimerOutlineColor { get; set; } = new(0, 0, 0, 1);
    public float DotTimerOutlineThickness { get; set; } = 1f;

    /// <summary>0 = above, 1 = right, 2 = left.</summary>
    public int DotIconPosition { get; set; }

    public bool OriginalTargetMarkerSortEnabled { get; set; }

    public bool OriginalTargetMarkerSortDescending { get; set; }

    public int[] OriginalTargetMarkerOrder { get; set; } = TargetMarkerSortOrder.DefaultOrder();

    public bool ShowBarrierAdjustedHp { get; set; }

    public bool PartySearchEnabled { get; set; } = true;

    /// <summary>Replacement name used for the local character while solo.</summary>
    public string PartySearchDisplayName { get; set; } = string.Empty;

    /// <summary>Whether a custom color is used for the local character's name while solo.</summary>
    public bool PartySearchUseCustomNameColor { get; set; }

    /// <summary>Custom RGBA color used for the local character's name while solo.</summary>
    public Vector4 PartySearchNameColor { get; set; } = Vector4.One;

    /// <summary>Whether target lines are drawn from the local player to eligible players.</summary>
    public bool PartySearchDrawTargetLines { get; set; } = true;

    /// <summary>RGBA color used for eligible-player target lines.</summary>
    public Vector4 PartySearchTargetLineColor { get; set; } = new(1f, 0.82f, 0.1f, 0.9f);

    /// <summary>Screen-space thickness of eligible-player target lines.</summary>
    public float PartySearchTargetLineThickness { get; set; } = 2f;
}












