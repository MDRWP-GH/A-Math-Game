using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMath.AI.Interfaces;
using AMath.Core.Assistance.Context;

namespace AMath.AI.Modes.StrategyCoach
{
    /// <summary>
    /// Strategy tips from scripted detectors only — no language-model call.
    /// Advice stays optional and never issues gameplay commands.
    /// </summary>
    public sealed class StrategyCoachMode : IAiAssistantMode
    {
        public const string ModeIdValue = "strategy_coach";

        private readonly IReadOnlyList<IStrategyTipDetector> _detectors;

        /// <summary>Creates the scripted Strategy Coach with the default tip set.</summary>
        public StrategyCoachMode()
            : this(ScriptedStrategyTipDetectors.CreateDefaultSet())
        {
        }

        /// <summary>Creates the coach with an explicit detector list (tests).</summary>
        public StrategyCoachMode(IReadOnlyList<IStrategyTipDetector> detectors)
        {
            _detectors = detectors ?? throw new ArgumentNullException(nameof(detectors));
        }

        /// <inheritdoc />
        public string ModeId => ModeIdValue;

        /// <inheritdoc />
        public Task<string> RespondAsync(
            string question,
            GameContextSnapshot context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();

            var tips = new List<string>(3);
            for (int i = 0; i < _detectors.Count && tips.Count < 3; i++)
            {
                if (_detectors[i].TryDetect(context, out string tip) && !string.IsNullOrWhiteSpace(tip))
                    tips.Add(tip.Trim());
            }

            if (tips.Count == 0)
            {
                return Task.FromResult(
                    "ยังไม่มีคำใบ้พิเศษจากสถานะตอนนี้ — ลองหาสมการที่ทั้งสองข้างของ '=' เท่ากัน "
                    + "และต่อกับกระดาน แล้วค่อยวิเคราะห์เองอีกครั้ง");
            }

            var text = new StringBuilder(256);
            text.AppendLine("คำใบ้จากสถานะกระดาน (สคริปต์):");
            for (int i = 0; i < tips.Count; i++)
                text.Append(i + 1).Append(") ").AppendLine(tips[i]);

            if (!string.IsNullOrWhiteSpace(question))
            {
                text.AppendLine();
                text.Append("คำถามของคุณถูกบันทึกไว้แล้ว แต่โหมดนี้ใช้การตรวจจับสถานะ ไม่ได้วิเคราะห์ข้อความอิสระ");
            }

            return Task.FromResult(text.ToString().TrimEnd());
        }
    }
}
