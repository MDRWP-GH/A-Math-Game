namespace AMath.Core
{
    /// <summary>
    /// Central rule constants for an A-Math match.
    /// Kept in one place so gameplay, validation, networking and tests
    /// never drift apart on fundamental values.
    /// </summary>
    public static class GameRules
    {
        /// <summary>Board is 15x15, identical layout to the official A-Math board.</summary>
        public const int BoardSize = 15;

        /// <summary>Number of tiles each player holds on their rack.</summary>
        public const int RackSize = 8;

        /// <summary>Center square coordinate; the first equation must cover it.</summary>
        public const int CenterX = 7;

        /// <summary>Center square coordinate; the first equation must cover it.</summary>
        public const int CenterY = 7;

        /// <summary>Minimum number of tiles in any valid equation (e.g. 1=1).</summary>
        public const int MinEquationLength = 3;

        /// <summary>Maximum digits that may be combined into a single number.</summary>
        public const int MaxNumberDigits = 3;

        /// <summary>Bonus awarded for using the entire rack in one turn.</summary>
        public const int FullRackBonus = 40;

        /// <summary>Default per-turn time limit in seconds (Rush preset).</summary>
        public const int DefaultTurnSeconds = 60;

        /// <summary>Lobby default before the host changes the turn-time row.</summary>
        public const TurnTimePreset DefaultTurnTimePreset = TurnTimePreset.Rush;

        /// <summary>Maps a lobby preset to the seconds written into <see cref="MatchConfig"/>.</summary>
        public static int TurnSecondsFor(TurnTimePreset preset) =>
            preset switch
            {
                TurnTimePreset.Rush => 60,
                TurnTimePreset.Short => 90,
                TurnTimePreset.Normal => 120,
                TurnTimePreset.Long => 180,
                _ => DefaultTurnSeconds
            };

        /// <summary>
        /// Full rounds of consecutive pass/exchange turns that end the match.
        /// The match ends once <c>playerCount * this</c> turns in a row have all
        /// been passes or exchanges — two complete times around the table, not
        /// two passes from one player.
        /// </summary>
        public const int ConsecutivePassRoundsToEnd = 2;

        /// <summary>Supported player range for a room.</summary>
        public const int MinPlayers = 2;

        /// <summary>Supported player range for a room.</summary>
        public const int MaxPlayers = 4;

        /// <summary>Number of teams in team mode.</summary>
        public const int TeamCount = 2;

        /// <summary>
        /// Individual and team matches require 2–4 human players; empty seats
        /// are no longer padded with AI in the lobby path.
        /// </summary>
        public static bool IsValidHumanRoster(int humanCount) =>
            humanCount >= MinPlayers && humanCount <= MaxPlayers;

        /// <summary>
        /// Team mode requires at least one player on each side. Supports 1v1,
        /// 1v2, 1v3 and 2v2 rosters.
        /// </summary>
        public static bool IsValidTeamSplit(int team0Count, int team1Count) =>
            team0Count >= 1 && team1Count >= 1;

    }
}
