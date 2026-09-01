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

        /// <summary>Default per-turn time limit in seconds. Host authoritative.</summary>
        public const int DefaultTurnSeconds = 60;

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
        public const int MaxPlayers = 8;

        /// <summary>Minimum seats required to start a team match (two teams of two).</summary>
        public const int MinTeamMatchPlayers = 4;

        /// <summary>Number of teams in team mode.</summary>
        public const int TeamCount = 2;

        /// <summary>
        /// True when <paramref name="seatCount"/> can be split into equal teams.
        /// The lobby uses this to explain why "start" is unavailable instead of
        /// letting the host press it and have the host silently refuse.
        /// </summary>
        public static bool IsValidTeamRoster(int seatCount) =>
            seatCount >= MinTeamMatchPlayers
            && seatCount <= MaxPlayers
            && seatCount % TeamCount == 0;

        /// <summary>
        /// Seats a match will actually have. Missing seats are filled with AI,
        /// so a solo host can play and team mode does not require four humans.
        /// The lobby and the host must agree on this number, otherwise the
        /// lobby offers a "start" the host then refuses.
        /// </summary>
        public static int PlannedSeatCount(MatchFormat format, int humanCount, int extraAiPlayers = 0)
        {
            int seats = humanCount + (extraAiPlayers > 0 ? extraAiPlayers : 0);
            int minimum = format == MatchFormat.Team ? MinTeamMatchPlayers : MinPlayers;
            if (seats < minimum)
                seats = minimum;

            if (format == MatchFormat.Team && seats % TeamCount != 0)
                seats++;

            return seats;
        }
    }
}
