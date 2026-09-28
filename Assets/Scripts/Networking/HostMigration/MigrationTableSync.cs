using System.Collections.Generic;
using AMath.Core.Events;
using AMath.Networking.RPC;
using Mirror;
using UnityEngine;

namespace AMath.Networking.HostMigration
{
    /// <summary>
    /// Scene network object that keeps the host-succession table replicated
    /// *while the host is still alive* — the whole point of host migration is
    /// that this information must already be on every client when the host
    /// vanishes.
    ///
    /// Server side: collects the RTT reports published by
    /// <see cref="NetworkPlayer"/>, pairs them with each connection's
    /// server-observed address (self-reported addresses are never trusted) and
    /// rebuilds the ranked SyncList at most every few seconds.
    /// Client side: mirrors every change into
    /// <see cref="HostReconnectManager"/>'s local cache, which survives the
    /// destruction of all networked objects on disconnect.
    /// </summary>
    public sealed class MigrationTableSync : NetworkBehaviour
    {
        #region Constants

        private const float RebuildInterval = 5f;

        #endregion

        #region Fields

        private readonly SyncList<MigrationCandidate> _candidates = new();
        private readonly Dictionary<int, double> _latestRtt = new();
        private readonly List<MigrationCandidate> _buildBuffer = new();

        private IEventBus _eventBus;
        private HostReconnectManager _migrationManager;
        private float _nextRebuild;

        #endregion

        #region Lifecycle

        public override void OnStartServer()
        {
            Resolve();
            if (NetworkContext.Services != null)
                NetworkContext.Services.RegisterOrReplace(this);

            _latestRtt.Clear();
            _eventBus.Subscribe<QualityReportReceivedEvent>(OnQualityReport);
        }

        public override void OnStopServer()
        {
            _eventBus?.Unsubscribe<QualityReportReceivedEvent>(OnQualityReport);
        }

        public override void OnStartClient()
        {
            Resolve();
            if (NetworkContext.Services != null)
                NetworkContext.Services.RegisterOrReplace(this);

            _candidates.OnChange += OnTableChanged;
            PushTableToCache();
        }

        public override void OnStopClient()
        {
            _candidates.OnChange -= OnTableChanged;
        }

        private void Resolve()
        {
            if (_eventBus != null || NetworkContext.Services == null) return;
            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _migrationManager = NetworkContext.Services.Resolve<HostReconnectManager>();
        }

        #endregion

        #region Server: table building

        private void OnQualityReport(QualityReportReceivedEvent evt)
        {
            _latestRtt[evt.PlayerId] = evt.RttSeconds * 1000.0;
        }

        [ServerCallback]
        private void Update()
        {
            if (Time.unscaledTime < _nextRebuild) return;
            _nextRebuild = Time.unscaledTime + RebuildInterval;
            RebuildTable();
        }

        [Server]
        private void RebuildTable()
        {
            _buildBuffer.Clear();

            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                // The host's own local connection can never be its successor.
                if (conn is LocalConnectionToClient) continue;

                if (conn.identity == null
                    || !conn.identity.TryGetComponent(out NetworkPlayer player)
                    || player.PlayerId < 0)
                    continue;

                _buildBuffer.Add(new MigrationCandidate
                {
                    PlayerId = player.PlayerId,
                    Address = StripPort(conn.address),
                    RttMs = _latestRtt.TryGetValue(player.PlayerId, out double rtt) ? rtt : double.MaxValue
                });
            }

            MigrationRanking.Sort(_buildBuffer);

            if (TableEquals(_buildBuffer)) return;

            _candidates.Clear();
            foreach (MigrationCandidate candidate in _buildBuffer)
                _candidates.Add(candidate);
        }

        private bool TableEquals(List<MigrationCandidate> other)
        {
            if (_candidates.Count != other.Count) return false;
            for (int i = 0; i < other.Count; i++)
            {
                if (_candidates[i].PlayerId != other[i].PlayerId
                    || _candidates[i].Address != other[i].Address)
                    return false;
            }

            return true;
        }

        /// <summary>KCP reports "ip:port"; only the IP is a valid reconnect target.</summary>
        private static string StripPort(string address)
        {
            if (string.IsNullOrEmpty(address)) return address;
            int colon = address.LastIndexOf(':');
            // Guard against IPv6 (multiple colons) — keep as-is in that case.
            return colon > 0 && address.IndexOf(':') == colon ? address[..colon] : address;
        }

        #endregion

        #region Client: cache mirroring

        private void OnTableChanged(SyncList<MigrationCandidate>.Operation op, int index, MigrationCandidate item)
        {
            PushTableToCache();
        }

        private void PushTableToCache()
        {
            if (_migrationManager == null) return;
            var copy = new List<MigrationCandidate>(_candidates.Count);
            for (int i = 0; i < _candidates.Count; i++)
                copy.Add(_candidates[i]);
            _migrationManager.UpdateCachedTable(copy);
        }

        #endregion
    }
}
