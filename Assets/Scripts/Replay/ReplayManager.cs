using System;
using AMath.Core;
using AMath.Core.Events;
using UnityEngine;

namespace AMath.Replay
{
    /// <summary>
    /// Records the running match as an event log.
    ///
    /// Listens to <see cref="TurnResolvedEvent"/> — which fires on the host and
    /// on every client for each accepted action — so every machine owns a
    /// complete replay at all times without any extra network traffic.
    /// Combined with the deterministic engine, the log reconstructs the whole
    /// match (see <see cref="ReplayReconstructor"/>).
    /// </summary>
    public sealed class ReplayManager : IDisposable
    {
        #region Fields

        private readonly IEventBus _eventBus;

        #endregion

        #region Properties

        /// <summary>The live log for the current match.</summary>
        public ReplayLog Log { get; private set; } = new();

        #endregion

        #region Construction

        public ReplayManager(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<MatchStartedEvent>(OnMatchStarted);
            _eventBus.Subscribe<TurnResolvedEvent>(OnTurnResolved);
        }

        #endregion

        #region Recording

        private void OnMatchStarted(MatchStartedEvent evt)
        {
            // Fresh log per match. (A restore overwrites this via ImportJson.)
            Log = new ReplayLog { Config = evt.Config };
        }

        private void OnTurnResolved(TurnResolvedEvent evt)
        {
            Log.Events.Add(ReplayEvent.FromRecord(evt.Record));
        }

        #endregion

        #region Import / export

        /// <summary>Serializes the current log (resync payloads, replay file export).</summary>
        public string ExportJson() => JsonUtility.ToJson(Log);

        /// <summary>Replaces the current log (reconnection resync, loading a save).</summary>
        public void ImportJson(string json)
        {
            ReplayLog imported = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ReplayLog>(json);
            if (imported != null)
                Log = imported;
        }

        /// <summary>Adopts a log object directly (loading a save file).</summary>
        public void Restore(ReplayLog log) => Log = log ?? new ReplayLog();

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _eventBus.Unsubscribe<MatchStartedEvent>(OnMatchStarted);
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnTurnResolved);
        }

        #endregion
    }
}
