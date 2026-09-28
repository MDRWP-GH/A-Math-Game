using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Networking;
using AMath.Networking.Room;
using AMath.Networking.RPC;
using Mirror;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>
    /// Presenter for the room lobby: member list, room code display and the
    /// host's "start match" action. Members are read from the replicated
    /// <see cref="NetworkPlayer"/> objects, so no extra roster sync exists.
    ///
    /// Spawn callbacks drive the refresh, but the roster is also re-read on a
    /// slow timer: a single missed event used to leave the lobby permanently
    /// empty, with no way for the player to recover short of leaving the room.
    /// </summary>
    public sealed class LobbyPresenter : MonoBehaviour
    {
        #region Constants

        /// <summary>Seconds between roster re-reads. Cheap: a handful of dictionary walks.</summary>
        private const float PollInterval = 0.3f;

        #endregion

        #region View-facing events

        /// <summary>Membership changed; rebuild the member list.</summary>
        public event Action<IReadOnlyList<NetworkPlayer>> MembersChanged;

        #endregion

        #region Fields

        private readonly List<NetworkPlayer> _members = new(GameRules.MaxPlayers);
        private IEventBus _eventBus;
        private RoomManager _roomManager;
        private RoomSession _session;
        private float _nextPoll;
        private int _rosterSignature;

        #endregion

        #region Properties

        /// <summary>Room code to display prominently ("share this with friends").</summary>
        public string RoomCode => _session?.RoomCode ?? string.Empty;

        /// <summary>Room display name.</summary>
        public string RoomName => _session?.RoomName ?? string.Empty;

        /// <summary>Current lobby roster (host first, then join order).</summary>
        public IReadOnlyList<NetworkPlayer> Members => _members;

        /// <summary>
        /// True when this machine runs the server. Taken from Mirror rather than
        /// <see cref="RoomSession.IsHost"/>, which is a cached copy that can
        /// outlive the session it described.
        /// </summary>
        public bool IsHost => NetworkServer.active;

        /// <summary>Seats the match would have (humans only in the lobby path).</summary>
        public int HumanCount => _members.Count;

        /// <summary>
        /// Only the host may start, and only when the roster satisfies the
        /// selected format's human-count and team-split rules.
        /// </summary>
        public bool CanStartMatch => StartBlockedReason == null;

        /// <summary>
        /// Localization key explaining why <see cref="CanStartMatch"/> is false,
        /// or null when the match can start. The host used to reject an
        /// unstartable roster silently, leaving a button that did nothing.
        /// </summary>
        public string StartBlockedReason
        {
            get
            {
                if (!IsHost)
                    return "ui.play.need_host";

                if (_members.Count < 1)
                    return "ui.play.need_players";

                if (!GameRules.IsValidHumanRoster(_members.Count))
                    return _members.Count < GameRules.MinPlayers
                        ? "ui.play.need_two_players"
                        : "ui.play.need_players";

                if (SelectedFormat == MatchFormat.Team)
                {
                    CountTeams(_members, out int team0, out int team1);
                    if (!GameRules.IsValidTeamSplit(team0, team1))
                        return "ui.play.need_both_teams";
                }

                return null;
            }
        }

        /// <summary>Colour the local player currently holds.</summary>
        public byte LocalColorId => LocalMember?.ColorId ?? PlayerColorPalette.FallbackId;

        /// <summary>Team the local player picked in the lobby (0 or 1).</summary>
        public byte LocalTeamId => LocalMember?.LobbyTeamId ?? 0;

        /// <summary>
        /// True when someone other than the local player already holds this
        /// colour, so the picker can grey it out instead of letting the player
        /// tap a swatch the host will refuse.
        /// </summary>
        public bool IsColorTaken(byte colorId)
        {
            NetworkPlayer local = LocalMember;
            foreach (NetworkPlayer member in _members)
            {
                if (member == null || member == local) continue;
                if (member.ColorId == colorId) return true;
            }

            return false;
        }

        /// <summary>Asks the host for a colour on behalf of the local player.</summary>
        public void RequestColor(byte colorId) => LocalMember?.RequestColor(colorId);

        /// <summary>Asks the host to place the local player on a team.</summary>
        public void RequestTeam(byte teamId) => LocalMember?.RequestTeam(teamId);

        private NetworkPlayer LocalMember
        {
            get
            {
                foreach (NetworkPlayer member in _members)
                {
                    if (member != null && member.isLocalPlayer) return member;
                }

                return null;
            }
        }

        private NetworkPlayer HostMember
        {
            get
            {
                foreach (NetworkPlayer member in _members)
                {
                    if (member != null && member.IsHost) return member;
                }

                return null;
            }
        }

        /// <summary>Match format selected by the host in the lobby.</summary>
        public MatchFormat SelectedFormat
        {
            get
            {
                if (IsHost)
                    return _session?.SelectedFormat ?? MatchFormat.Individual;

                NetworkPlayer host = HostMember;
                if (host != null)
                    return host.LobbyMatchFormat;

                return _session?.SelectedFormat ?? MatchFormat.Individual;
            }
            set
            {
                if (SelectedFormat == value)
                    return;

                if (_session != null)
                    _session.SelectedFormat = value;

                LocalMember?.RequestLobbyMatchFormat(value);
            }
        }

        /// <summary>Turn-time preset selected by the host in the lobby.</summary>
        public TurnTimePreset SelectedTurnTimePreset
        {
            get
            {
                if (IsHost)
                    return _session?.SelectedTurnTimePreset ?? GameRules.DefaultTurnTimePreset;

                NetworkPlayer host = HostMember;
                if (host != null)
                    return host.LobbyTurnTimePreset;

                return _session?.SelectedTurnTimePreset ?? GameRules.DefaultTurnTimePreset;
            }
            set
            {
                if (SelectedTurnTimePreset == value)
                    return;

                if (_session != null)
                    _session.SelectedTurnTimePreset = value;

                LocalMember?.RequestLobbyTurnTimePreset(value);
            }
        }

        #endregion

        #region Lifecycle

        private void Start()
        {
            if (NetworkContext.Services == null)
            {
                Debug.LogError("[Lobby] Services are not installed; the roster cannot update.");
                return;
            }

            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _roomManager = NetworkContext.Services.Resolve<RoomManager>();
            _session = NetworkContext.Services.Resolve<RoomSession>();

            _eventBus.Subscribe<PlayerRosterChangedEvent>(OnRosterChanged);
            RefreshMembers();
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<PlayerRosterChangedEvent>(OnRosterChanged);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollInterval;

            if (!NetworkServer.active && !NetworkClient.active) return;
            RefreshIfRosterChanged();
        }

        private void OnRosterChanged(PlayerRosterChangedEvent evt) => RefreshMembers();

        /// <summary>Rebuilds the roster from live Mirror connections.</summary>
        public void RefreshMembers()
        {
            Collect();
            SyncFormatFromHost();
            SyncTurnTimeFromHost();
            _rosterSignature = ComputeSignature(_members);
            MembersChanged?.Invoke(_members);
        }

        /// <summary>
        /// Re-reads the roster and only notifies the view when it actually
        /// differs, so the poll does not rebuild the member cards every frame.
        /// </summary>
        private void RefreshIfRosterChanged()
        {
            Collect();
            SyncFormatFromHost();
            SyncTurnTimeFromHost();
            int signature = ComputeSignature(_members);
            if (signature == _rosterSignature) return;

            _rosterSignature = signature;
            MembersChanged?.Invoke(_members);
        }

        private void Collect()
        {
            _members.Clear();

            if (NetworkServer.active)
                CollectFromServerConnections(_members);
            else if (NetworkClient.active)
                CollectFromClientSpawned(_members);

            AppendLocalPlayerIfMissing(_members);
            _members.RemoveAll(player => player == null);
        }

        /// <summary>
        /// Cheap fingerprint of everything the member list renders, so a poll
        /// can tell "nothing changed" from "someone joined or was renamed".
        /// </summary>
        private static int ComputeSignature(List<NetworkPlayer> members)
        {
            unchecked
            {
                int hash = 17 + members.Count;
                foreach (NetworkPlayer player in members)
                {
                    hash = (hash * 31) + (int)player.netId;
                    hash = (hash * 31) + (player.DisplayName?.GetHashCode() ?? 0);
                    hash = (hash * 31) + player.PlayerId;
                    hash = (hash * 31) + (player.IsHost ? 1 : 0);
                    hash = (hash * 31) + player.ColorId;
                    hash = (hash * 31) + player.LobbyTeamId;
                    hash = (hash * 31) + (int)player.LobbyMatchFormat;
                    hash = (hash * 31) + (int)player.LobbyTurnTimePreset;
                }

                return hash;
            }
        }

        private static void CollectFromServerConnections(List<NetworkPlayer> members)
        {
            // Same ordering as RoomManager.StartMatch: host first, then join order.
            var ordered = new List<NetworkConnectionToClient>(NetworkServer.connections.Values);
            ordered.Sort((a, b) => a.connectionId.CompareTo(b.connectionId));

            if (NetworkServer.localConnection?.identity != null
                && NetworkServer.localConnection.identity.TryGetComponent(out NetworkPlayer hostPlayer))
            {
                members.Add(hostPlayer);
            }

            foreach (NetworkConnectionToClient conn in ordered)
            {
                if (conn.identity != null
                    && conn.identity.TryGetComponent(out NetworkPlayer player)
                    && !members.Contains(player))
                {
                    members.Add(player);
                }
            }
        }

        private static void CollectFromClientSpawned(List<NetworkPlayer> members)
        {
            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity != null
                    && identity.TryGetComponent(out NetworkPlayer player)
                    && !members.Contains(player))
                {
                    members.Add(player);
                }
            }
        }

        private static void AppendLocalPlayerIfMissing(List<NetworkPlayer> members)
        {
            if (NetworkClient.localPlayer != null
                && NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer local)
                && !members.Contains(local))
            {
                members.Insert(0, local);
            }
        }

        /// <summary>
        /// Copies the host's replicated format onto the local session so
        /// rematch and other session readers see the same choice clients do.
        /// </summary>
        private void SyncFormatFromHost()
        {
            NetworkPlayer host = HostMember;
            if (host == null || _session == null)
                return;

            _session.SelectedFormat = host.LobbyMatchFormat;
        }

        /// <summary>
        /// Copies the host's replicated turn-time preset onto the local session
        /// so match start and rematch readers see the same choice clients do.
        /// </summary>
        private void SyncTurnTimeFromHost()
        {
            NetworkPlayer host = HostMember;
            if (host == null || _session == null)
                return;

            _session.SelectedTurnTimePreset = host.LobbyTurnTimePreset;
        }

        #endregion

        #region Internals

        private static void CountTeams(IReadOnlyList<NetworkPlayer> members, out int team0Count, out int team1Count)
        {
            team0Count = 0;
            team1Count = 0;

            foreach (NetworkPlayer member in members)
            {
                if (member == null) continue;
                if (member.LobbyTeamId == 0)
                    team0Count++;
                else
                    team1Count++;
            }
        }

        #endregion

        #region View commands

        /// <summary>Host action: assign seats and start the match.</summary>
        public void StartMatch() => _roomManager.StartMatch(SelectedFormat);

        /// <summary>Leaves the room (both roles).</summary>
        public void LeaveRoom() => _roomManager.LeaveRoom();

        #endregion
    }
}
