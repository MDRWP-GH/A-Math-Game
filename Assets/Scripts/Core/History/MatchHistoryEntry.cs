using System;
using System.Collections.Generic;

namespace AMath.Core.History
{
    /// <summary>Summary of one finished match for the history browser.</summary>
    [Serializable]
    public sealed class MatchHistoryEntry
    {
        public string MatchId;
        public long StartedUtcTicks;
        public long FinishedUtcTicks;
        public string RoomName;
        public string RoomCode;
        public MatchFormat Format;
        public int DurationSeconds;
        public int TurnCount;
        public string WinnerLabel;
        public int WinnerScore;
        public string ReplayFileName;
        public string StandingsSummary;
        public string LocalPlayerName;
        public int LocalPlayerScore;
        public bool HasLocalPlayer;
        public bool DidWin;
        public bool IsDraw;
        public List<PlayerResult> Players = new();
        public List<TeamResult> Teams = new();
    }

    [Serializable]
    public sealed class MatchHistoryIndex
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public List<MatchHistoryEntry> Entries = new();
    }
}
