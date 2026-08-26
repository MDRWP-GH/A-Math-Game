using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Networking.Room;
using AMath.Utilities;
using Mirror;
using UnityEngine;

namespace AMath.Networking.Messages
{
    /// <summary>
    /// Gatekeeper for every incoming connection. Runs *before* any object is
    /// spawned for the client, so untrusted peers are rejected without ever
    /// touching game state.
    ///
    /// The host rejects when:
    ///  - the game version differs (protocol/rules compatibility),
    ///  - the room code does not match this room,
    ///  - the room is full,
    ///  - a match is running and the GUID does not belong to a seated player
    ///    (only reconnections are allowed mid-match),
    ///  - the GUID is already connected (duplicate / impersonation attempt).
    /// </summary>
    public sealed class RoomAuthenticator : NetworkAuthenticator
    {
        #region Limits

        /// <summary>
        /// Upper bounds on every string a client can put in the very first
        /// message it sends. Without them an unauthenticated peer chooses how
        /// much the host allocates.
        /// </summary>
        private const int MaxGameVersionLength = 32;
        private const int MaxRoomCodeLength = 16;
        private const int MaxPersistentGuidLength = 64;
        private const int MaxDisplayNameLength = 32;
        private const int MaxReconnectTokenLength = 64;

        #endregion

        #region Dependencies (injected by the composition root)

        private RoomSession _session;
        private PlayerManager _playerManager;
        private GameManager _gameManager;
        private IEventBus _eventBus;

        /// <summary>
        /// Tokens handed out to each GUID during this hosting session. Absent
        /// entries mean "we never authenticated this client" — which is the
        /// normal state for a host that just took over after migration, so a
        /// missing entry falls back to GUID-only identification rather than
        /// locking survivors out of their own match.
        /// </summary>
        private readonly Dictionary<string, string> _issuedTokens = new();

        /// <summary>Scratch buffer: eviction must not mutate the connection dictionary while iterating it.</summary>
        private static readonly List<NetworkConnectionToClient> _staleConnections = new();

        /// <summary>Injects domain dependencies. Must be called before hosting.</summary>
        public void Configure(
            RoomSession session,
            PlayerManager playerManager,
            GameManager gameManager,
            IEventBus eventBus)
        {
            _session = session;
            _playerManager = playerManager;
            _gameManager = gameManager;
            _eventBus = eventBus;
        }

        #endregion

        #region Server side

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequest, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AuthRequestMessage>();
            _issuedTokens.Clear();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            // Passive: wait for the client's AuthRequestMessage.
        }

        private void OnAuthRequest(NetworkConnectionToClient conn, AuthRequestMessage message)
        {
            string rejection = Evaluate(conn, message, out AuthenticatedIdentity identity);
            if (rejection == null)
            {
                // Rotate on every accepted connection: a token sniffed off the
                // wire stops working as soon as its owner reconnects once.
                string token = GenerateReconnectToken();
                _issuedTokens[identity.PersistentGuid] = token;

                conn.authenticationData = identity;
                conn.Send(new AuthResponseMessage { Approved = true, ReconnectToken = token });
                ServerAccept(conn);
            }
            else
            {
                Debug.LogWarning(
                    $"[Auth] Rejected connection {conn.connectionId} " +
                    $"(name '{message.DisplayName}', guid '{message.PersistentGuid}', " +
                    $"room '{message.RoomCode}' vs host '{_session.RoomCode}'): {rejection}");
                conn.Send(new AuthResponseMessage { Approved = false, Reason = rejection });
                // Give the transport a moment to flush the reason before closing.
                StartCoroutine(DelayedReject(conn));
            }
        }

        /// <summary>Returns null when accepted, otherwise the rejection reason.</summary>
        private string Evaluate(
            NetworkConnectionToClient conn,
            AuthRequestMessage message,
            out AuthenticatedIdentity identity)
        {
            identity = null;

            if (Exceeds(message.GameVersion, MaxGameVersionLength)
                || Exceeds(message.RoomCode, MaxRoomCodeLength)
                || Exceeds(message.PersistentGuid, MaxPersistentGuidLength)
                || Exceeds(message.DisplayName, MaxDisplayNameLength)
                || Exceeds(message.ReconnectToken, MaxReconnectTokenLength))
                return "Malformed authentication request.";

            if (message.GameVersion != Application.version)
                return $"Version mismatch (host {Application.version}, you {message.GameVersion}).";

            if (string.IsNullOrEmpty(message.PersistentGuid) || string.IsNullOrEmpty(message.DisplayName))
                return "Invalid identity.";

            if (IsAiSeatGuid(message.PersistentGuid))
                return "AI seats cannot connect over the network.";

            if (!string.Equals(message.RoomCode, _session.RoomCode, System.StringComparison.OrdinalIgnoreCase))
                return "Wrong room code.";

            // The host occupies a seat through its own local connection, which
            // can never be evicted without tearing down the room. Two copies of
            // the same install share one PlayerPrefs GUID, so say so plainly
            // instead of reporting a generic "already connected".
            if (conn != NetworkServer.localConnection
                && NetworkServer.localConnection?.authenticationData is AuthenticatedIdentity hostIdentity
                && hostIdentity.PersistentGuid == message.PersistentGuid)
                return "This identity is already used by the host (same installation).";

            // Finished matches are not resumable, so treat them like the lobby
            // instead of letting a stale client reconnect into a closed match.
            bool matchRunning = _gameManager.Config != null
                && _gameManager.Phase != MatchPhase.Lobby
                && _gameManager.Phase != MatchPhase.Finished;

            // The seat check runs BEFORE the duplicate-GUID handling: a client
            // that dropped is still in NetworkServer.connections until the
            // transport times it out, so treating "same GUID" as a hard
            // rejection would make every reconnect fail.
            if (matchRunning)
            {
                // Mid-match, only players who already own a seat may (re)join.
                PlayerState seat = _playerManager.FindByGuid(message.PersistentGuid);
                if (seat == null)
                    return "Match already in progress.";

                if (seat.IsAi)
                    return "This seat is controlled by the host AI.";

                if (_issuedTokens.TryGetValue(message.PersistentGuid, out string expectedToken)
                    && !FixedTimeEquals(expectedToken, message.ReconnectToken))
                    return "Reconnection token does not match this seat.";

                // The token proved ownership, so the older connection for this
                // GUID is a leftover of the same player and is dropped.
                DisconnectStaleConnections(conn, message.PersistentGuid);

                identity = new AuthenticatedIdentity
                {
                    PersistentGuid = message.PersistentGuid,
                    DisplayName = message.DisplayName,
                    IsReconnection = true,
                    ExistingPlayerId = seat.PlayerId
                };
                return null;
            }

            // In the lobby a seat holds no state worth protecting, so the newest
            // connection wins and the stale one is evicted.
            int evicted = DisconnectStaleConnections(conn, message.PersistentGuid);

            // The pending connection is already counted in NetworkServer.connections,
            // so "full" means the count would EXCEED the seat limit. Connections
            // we just evicted are still listed until the transport removes them.
            if (NetworkServer.connections.Count - evicted > _session.MaxPlayers)
                return "Room is full.";

            identity = new AuthenticatedIdentity
            {
                PersistentGuid = message.PersistentGuid,
                DisplayName = message.DisplayName
            };
            return null;
        }

        /// <summary>
        /// Drops every other connection that claims <paramref name="persistentGuid"/>
        /// and returns how many were dropped. The host identifies a player by
        /// GUID, so two live connections sharing one would fight over the seat.
        /// </summary>
        private static int DisconnectStaleConnections(NetworkConnectionToClient conn, string persistentGuid)
        {
            _staleConnections.Clear();
            foreach (NetworkConnectionToClient existing in NetworkServer.connections.Values)
            {
                if (existing == conn || existing == NetworkServer.localConnection) continue;
                if (existing.authenticationData is AuthenticatedIdentity other
                    && other.PersistentGuid == persistentGuid)
                    _staleConnections.Add(existing);
            }

            foreach (NetworkConnectionToClient stale in _staleConnections)
            {
                Debug.LogWarning(
                    $"[Auth] Evicting stale connection {stale.connectionId} for guid '{persistentGuid}'.");
                stale.Disconnect();
            }

            return _staleConnections.Count;
        }

        private static bool IsAiSeatGuid(string persistentGuid) =>
            !string.IsNullOrEmpty(persistentGuid)
            && persistentGuid.StartsWith("ai:", System.StringComparison.Ordinal);

        private static bool Exceeds(string value, int maxLength) =>
            value != null && value.Length > maxLength;

        private static string GenerateReconnectToken()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            return System.Convert.ToBase64String(bytes);
        }

        /// <summary>Length-independent comparison so a mismatch leaks no timing signal.</summary>
        private static bool FixedTimeEquals(string expected, string provided)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
                return false;
            if (expected.Length != provided.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < expected.Length; i++)
                difference |= expected[i] ^ provided[i];

            return difference == 0;
        }

        private IEnumerator DelayedReject(NetworkConnectionToClient conn)
        {
            yield return new WaitForSeconds(0.5f);
            ServerReject(conn);
        }

        #endregion

        #region Client side

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponse, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        public override void OnClientAuthenticate()
        {
            NetworkClient.Send(new AuthRequestMessage
            {
                GameVersion = Application.version,
                RoomCode = _session.RoomCode,
                PersistentGuid = LocalIdentity.PersistentGuid,
                DisplayName = LocalIdentity.DisplayName,
                ReconnectToken = _session.ReconnectToken
            });
        }

        private void OnAuthResponse(AuthResponseMessage message)
        {
            if (message.Approved)
            {
                _session.ReconnectToken = message.ReconnectToken;
                ClientAccept();
            }
            else
            {
                Debug.LogWarning($"[Auth] Rejected by host: {message.Reason}");

                // Published before ClientReject so the reason is already known
                // when the resulting disconnect reaches the recovery pipeline,
                // which must not mistake a refused join for a lost connection.
                _eventBus?.Publish(new ConnectionRejectedEvent { Reason = message.Reason });
                ClientReject();
            }
        }

        #endregion
    }
}
