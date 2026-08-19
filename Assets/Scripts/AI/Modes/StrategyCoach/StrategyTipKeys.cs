namespace AMath.AI.Modes.StrategyCoach
{
    /// <summary>
    /// Localization keys for the scripted Strategy Coach. The coach runs
    /// offline, so its advice is authored text rather than model output — but
    /// it still has to follow the player's language setting, which means the
    /// detectors emit keys instead of sentences.
    /// </summary>
    public static class StrategyTipKeys
    {
        /// <summary>Header above the tip list.</summary>
        public const string Header = "ai.strategy.header";

        /// <summary>Shown when no detector matched.</summary>
        public const string NoTips = "ai.strategy.no_tips";

        /// <summary>Explains that this mode ignores free-text questions.</summary>
        public const string QuestionIgnored = "ai.strategy.question_ignored";

        /// <summary>Last action was rejected; takes the reason as {0}.</summary>
        public const string Rejection = "ai.strategy.rejection";

        /// <summary>Opening move must cover the centre square.</summary>
        public const string FirstMoveCenter = "ai.strategy.first_move_center";

        /// <summary>Hand has no equals sign.</summary>
        public const string MissingEquals = "ai.strategy.missing_equals";

        /// <summary>Bag is nearly empty.</summary>
        public const string LowBag = "ai.strategy.low_bag";

        /// <summary>Hand is short on numbers.</summary>
        public const string FewNumbers = "ai.strategy.few_numbers";

        /// <summary>Hand can build an equation but the player has not placed one.</summary>
        public const string EqualsButStuck = "ai.strategy.equals_but_stuck";
    }
}
