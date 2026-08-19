using System;
using System.Collections.Generic;

namespace AMath.Core.History
{
    /// <summary>Summary of one finished match for the history browser.</summary>
    [Serializable]
    public sealed class MatchHistoryEntry
    {
        public string MatchId;
        public long FinishedUtcTicks;
        public string RoomName;
        public string RoomCode;
        public MatchFormat Format;
        public int DurationSeconds;
        public int TurnCount;
        public string WinnerLabel;
        public int WinnerScore;
        public string AccountUsername;
        public string ReplayFileName;
        public string StandingsSummary;
    }

    [Serializable]
    public sealed class MatchHistoryIndex
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public List<MatchHistoryEntry> Entries = new();
    }
}
