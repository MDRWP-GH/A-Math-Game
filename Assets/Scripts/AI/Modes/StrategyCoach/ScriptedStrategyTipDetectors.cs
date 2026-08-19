using System;
using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.AI.Modes.StrategyCoach
{
    /// <summary>Built-in scripted strategy tips (no LLM).</summary>
    public static class ScriptedStrategyTipDetectors
    {
        /// <summary>Default detector set in priority order.</summary>
        public static IReadOnlyList<IStrategyTipDetector> CreateDefaultSet(ILocalizedTextProvider text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            return new IStrategyTipDetector[]
            {
                new RejectionTipDetector(text),
                new FirstMoveCenterDetector(text),
                new MissingEqualsDetector(text),
                new LowBagDetector(text),
                new FewNumbersDetector(text),
                new HasEqualsButStuckDetector(text)
            };
        }

        private abstract class LocalizedDetector : IStrategyTipDetector
        {
            protected LocalizedDetector(ILocalizedTextProvider text)
            {
                Text = text;
            }

            protected ILocalizedTextProvider Text { get; }

            public abstract string DetectorId { get; }

            public abstract bool TryDetect(GameContextSnapshot context, out string tip);
        }

        private sealed class RejectionTipDetector : LocalizedDetector
        {
            public RejectionTipDetector(ILocalizedTextProvider text) : base(text) { }

            public override string DetectorId => "rejection";

            public override bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (string.IsNullOrWhiteSpace(context.LastCommandRejectionReason))
                    return false;

                tip = string.Format(
                    Text.GetText(StrategyTipKeys.Rejection),
                    context.LastCommandRejectionReason);
                return true;
            }
        }

        private sealed class FirstMoveCenterDetector : LocalizedDetector
        {
            public FirstMoveCenterDetector(ILocalizedTextProvider text) : base(text) { }

            public override string DetectorId => "first_move_center";

            public override bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.BoardCells != null && context.BoardCells.Count > 0)
                    return false;
                if (context.MatchPhase != MatchPhase.Playing)
                    return false;

                tip = Text.GetText(StrategyTipKeys.FirstMoveCenter);
                return true;
            }
        }

        private sealed class MissingEqualsDetector : LocalizedDetector
        {
            public MissingEqualsDetector(ILocalizedTextProvider text) : base(text) { }

            public override string DetectorId => "missing_equals";

            public override bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.LocalPlayerHand == null) return false;
                if (HandCanMakeEquals(context.LocalPlayerHand)) return false;

                tip = Text.GetText(StrategyTipKeys.MissingEquals);
                return true;
            }
        }

        private sealed class LowBagDetector : LocalizedDetector
        {
            public LowBagDetector(ILocalizedTextProvider text) : base(text) { }

            public override string DetectorId => "low_bag";

            public override bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.TilesRemainingInBag > 12) return false;

                tip = Text.GetText(StrategyTipKeys.LowBag);
                return true;
            }
        }

        private sealed class FewNumbersDetector : LocalizedDetector
        {
            public FewNumbersDetector(ILocalizedTextProvider text) : base(text) { }

            public override string DetectorId => "few_numbers";

            public override bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.LocalPlayerHand == null) return false;

                int numbers = 0;
                for (int i = 0; i < context.LocalPlayerHand.Count; i++)
                {
                    byte id = context.LocalPlayerHand[i];
                    if (IsNumber(id) || id == Blank) numbers++;
                }

                if (numbers >= 3) return false;
                tip = Text.GetText(StrategyTipKeys.FewNumbers);
                return true;
            }
        }

        private sealed class HasEqualsButStuckDetector : LocalizedDetector
        {
            public HasEqualsButStuckDetector(ILocalizedTextProvider text) : base(text) { }

            public override string DetectorId => "equals_but_stuck";

            public override bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.LocalPlayerHand == null) return false;
                if (!HandCanMakeEquals(context.LocalPlayerHand)) return false;
                if (context.BoardCells == null || context.BoardCells.Count == 0) return false;

                int numbers = 0;
                int ops = 0;
                for (int i = 0; i < context.LocalPlayerHand.Count; i++)
                {
                    byte id = context.LocalPlayerHand[i];
                    if (IsNumber(id) || id == Blank) numbers++;
                    if (IsResolvedOperator(id) || id == PlusOrMinus || id == TimesOrDivide) ops++;
                }

                if (numbers < 2 || ops < 1) return false;

                tip = Text.GetText(StrategyTipKeys.EqualsButStuck);
                return true;
            }
        }

        private static bool HandCanMakeEquals(IReadOnlyList<byte> hand)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                byte id = hand[i];
                if (id == EqualsSign || id == Blank) return true;
            }

            return false;
        }
    }
}
