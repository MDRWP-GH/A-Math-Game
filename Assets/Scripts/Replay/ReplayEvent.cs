using System;
using AMath.Core;

namespace AMath.Replay
{
    /// <summary>
    /// One recorded action in a replay — the JSON-friendly twin of
    /// <see cref="TurnRecord"/> (payload stored as Base64 to keep files small
    /// and human-inspectable). No video, no per-frame data: an entire match is
    /// typically a few kilobytes.
    /// </summary>
    [Serializable]
    public sealed class ReplayEvent
    {
        public int Turn;
        public int PlayerId;
        public byte CommandType;
        public string PayloadBase64;
        public int ScoreDelta;
        public long TimestampUtcTicks;

        /// <summary>Converts an authoritative record into a replay event.</summary>
        public static ReplayEvent FromRecord(TurnRecord record) => new()
        {
            Turn = record.TurnNumber,
            PlayerId = record.PlayerId,
            CommandType = record.CommandType,
            PayloadBase64 = Convert.ToBase64String(record.CommandPayload ?? Array.Empty<byte>()),
            ScoreDelta = record.ScoreDelta,
            TimestampUtcTicks = record.TimestampUtcTicks
        };

        /// <summary>Converts back into a record for re-execution.</summary>
        public TurnRecord ToRecord() => new()
        {
            TurnNumber = Turn,
            PlayerId = PlayerId,
            CommandType = CommandType,
            CommandPayload = Convert.FromBase64String(PayloadBase64 ?? string.Empty),
            ScoreDelta = ScoreDelta,
            TimestampUtcTicks = TimestampUtcTicks
        };
    }

    /// <summary>
    /// A complete replay: the match header (seed + roster + rules) plus every
    /// accepted action in order. Because the engine is deterministic, this is
    /// sufficient to reconstruct the entire match exactly — board, racks, bag,
    /// scores — at any turn.
    /// </summary>
    [Serializable]
    public sealed class ReplayLog
    {
        /// <summary>Match header; replaying starts from this config.</summary>
        public MatchConfig Config;

        /// <summary>Accepted actions in execution order.</summary>
        public System.Collections.Generic.List<ReplayEvent> Events = new();
    }
}
