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

        /// <summary>Consecutive pass/exchange turns (per player) that end the match.</summary>
        public const int ConsecutivePassRoundsToEnd = 2;

        /// <summary>Supported player range for a room.</summary>
        public const int MinPlayers = 2;

        /// <summary>Supported player range for a room.</summary>
        public const int MaxPlayers = 8;
    }
}
