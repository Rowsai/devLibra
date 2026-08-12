using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace devLibra;

/// <summary>
/// Matches nearby object-table players with Player Search entries.  Unlike an
/// object's relation flags, Player Search states describe the player's own
/// party or alliance membership.
/// </summary>
internal static unsafe class NearbyPlayerSearch
{
    private const byte MaximumDistance = 100;
    private const long RefreshIntervalMilliseconds = 3000;
    private static long lastRefreshRequestAt;

    private const InfoProxyCommonList.CharacterData.OnlineStatus PartyMembershipFlags =
        InfoProxyCommonList.CharacterData.OnlineStatus.AllianceLeader
        | InfoProxyCommonList.CharacterData.OnlineStatus.AlliancePartyLeader
        | InfoProxyCommonList.CharacterData.OnlineStatus.AlliancePartyMember
        | InfoProxyCommonList.CharacterData.OnlineStatus.PartyLeader
        | InfoProxyCommonList.CharacterData.OnlineStatus.PartyMember
        | InfoProxyCommonList.CharacterData.OnlineStatus.PartyLeaderCrossWorld
        | InfoProxyCommonList.CharacterData.OnlineStatus.PartyMemberCrossWorld;

    /// <summary>
    /// Requests the current Player Search result set at a controlled interval.
    /// The response fills the same proxy read by <see cref="GetNearbyPlayers"/>.
    /// </summary>
    internal static void RefreshOnlineStates()
    {
        var now = Environment.TickCount64;
        if (now - lastRefreshRequestAt < RefreshIntervalMilliseconds)
            return;

        try
        {
            var playerSearch = GetPlayerSearchProxy();
            if (playerSearch == null)
                return;

            // RequestData returns false while the client cannot send a new
            // request (for example, while one is in progress).  Throttle both
            // outcomes so the framework update never retries every frame.
            lastRefreshRequestAt = now;
            playerSearch->RequestData();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Failed to refresh Player Search data.");
        }
    }

    internal static IReadOnlyList<NearbyPlayerSearchEntry> GetNearbyPlayers()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null)
            return Array.Empty<NearbyPlayerSearchEntry>();

        var searchStates = GetPlayerSearchStates();

        return Plugin.ObjectTable
            .OfType<IPlayerCharacter>()
            .Where(player => player.GameObjectId != localPlayer.GameObjectId)
            .Where(player => player.CurrentDistance <= MaximumDistance)
            .Where(player => !string.IsNullOrWhiteSpace(player.Name.TextValue))
            .Select(player => CreateEntry(player, searchStates))
            .OrderBy(entry => entry.Player.CurrentDistance)
            .ThenBy(entry => entry.Player.Name.TextValue, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static NearbyPlayerSearchEntry CreateEntry(
        IPlayerCharacter player,
        IReadOnlyDictionary<PlayerSearchKey, InfoProxyCommonList.CharacterData.OnlineStatus> searchStates)
    {
        var hasSearchState = searchStates.TryGetValue(CreateKey(player), out var state);
        var isOnline = hasSearchState
            && (state & InfoProxyCommonList.CharacterData.OnlineStatus.Online) != 0;
        var isSolo = isOnline && (state & PartyMembershipFlags) == 0;

        return new NearbyPlayerSearchEntry(player, hasSearchState, isOnline, isSolo);
    }

    private static Dictionary<PlayerSearchKey, InfoProxyCommonList.CharacterData.OnlineStatus> GetPlayerSearchStates()
    {
        var states = new Dictionary<PlayerSearchKey, InfoProxyCommonList.CharacterData.OnlineStatus>();

        try
        {
            var playerSearch = GetPlayerSearchProxy();
            if (playerSearch == null)
                return states;

            var commonList = (InfoProxyCommonList*)playerSearch;
            foreach (var character in commonList->CharDataSpan)
            {
                var name = character.NameString;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                states[new PlayerSearchKey(name, character.HomeWorld)] = character.State;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Failed to read Player Search data.");
        }

        return states;
    }

    private static InfoProxySearch* GetPlayerSearchProxy()
    {
        var infoModule = InfoModule.Instance();
        if (infoModule == null)
            return null;

        var proxy = infoModule->GetInfoProxyById(InfoProxyId.PlayerSearch);
        return (InfoProxySearch*)proxy;
    }

    private static PlayerSearchKey CreateKey(IPlayerCharacter player)
        => new(player.Name.TextValue, checked((ushort)player.HomeWorld.RowId));

    private readonly record struct PlayerSearchKey(string Name, ushort HomeWorld);
}

internal sealed record NearbyPlayerSearchEntry(
    IPlayerCharacter Player,
    bool HasPlayerSearchState,
    bool IsOnline,
    bool IsSolo);
