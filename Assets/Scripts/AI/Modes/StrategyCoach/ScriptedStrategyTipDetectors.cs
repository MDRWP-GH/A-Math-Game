using System.Collections.Generic;
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
        public static IReadOnlyList<IStrategyTipDetector> CreateDefaultSet() => new IStrategyTipDetector[]
        {
            new RejectionTipDetector(),
            new FirstMoveCenterDetector(),
            new MissingEqualsDetector(),
            new LowBagDetector(),
            new FewNumbersDetector(),
            new HasEqualsButStuckDetector()
        };

        private sealed class RejectionTipDetector : IStrategyTipDetector
        {
            public string DetectorId => "rejection";

            public bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (string.IsNullOrWhiteSpace(context.LastCommandRejectionReason))
                    return false;

                tip = "การวางล่าสุดไม่ผ่าน: " + context.LastCommandRejectionReason
                      + " ลองจัดสมการใหม่ให้ทั้งสองข้างของ '=' เท่ากัน และต่อกับกระดานที่มีอยู่";
                return true;
            }
        }

        private sealed class FirstMoveCenterDetector : IStrategyTipDetector
        {
            public string DetectorId => "first_move_center";

            public bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.BoardCells != null && context.BoardCells.Count > 0)
                    return false;
                if (context.MatchPhase != MatchPhase.Playing)
                    return false;

                tip = "ตาแรกต้องวางสมการให้ครอบช่องกลางกระดาน และมีความยาวอย่างน้อย 3 ชิ้น เช่น 1+2=3";
                return true;
            }
        }

        private sealed class MissingEqualsDetector : IStrategyTipDetector
        {
            public string DetectorId => "missing_equals";

            public bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.LocalPlayerHand == null) return false;
                if (HandCanMakeEquals(context.LocalPlayerHand)) return false;

                tip = "ในมือยังไม่มี '=' (หรือใบว่างที่จะใช้แทน) — ลองแลกไทล์ หรือรอจังหวะที่มี '=' ก่อนวางสมการยาว";
                return true;
            }
        }

        private sealed class LowBagDetector : IStrategyTipDetector
        {
            public string DetectorId => "low_bag";

            public bool TryDetect(GameContextSnapshot context, out string tip)
            {
                tip = null;
                if (context.TilesRemainingInBag > 12) return false;

                tip = "ถุงไทล์เหลือน้อยแล้ว — ระวังไทล์ติดมือตอนจบเกม จะถูกหักคะแนน และคนที่หมดมือก่อนจะได้โบนัสจากของคนอื่น";
                return true;
            }
        }

        private sealed class FewNumbersDetector : IStrategyTipDetector
        {
            public string DetectorId => "few_numbers";

            public bool TryDetect(GameContextSnapshot context, out string tip)
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
                tip = "ตัวเลขในมือน้อย — แลกไทล์บางใบ หรือหาสมการสั้นๆ อย่าง ก=ก ถ้ามีเลขคู่และ '='";
                return true;
            }
        }

        private sealed class HasEqualsButStuckDetector : IStrategyTipDetector
        {
            public string DetectorId => "equals_but_stuck";

            public bool TryDetect(GameContextSnapshot context, out string tip)
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

                tip = "มีวัตถุดิบพอสำหรับสมการแบบ ก+ข=ค — ลองวางต่อจากไทล์บนกระดานในแถวหรือคอลัมน์เดียว ให้ต่อเนื่องไม่มีช่องว่าง";
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
