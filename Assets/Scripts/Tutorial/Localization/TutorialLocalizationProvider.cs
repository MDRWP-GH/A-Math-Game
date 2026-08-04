using System.Collections.Generic;
using AMath.Core.Assistance;

namespace AMath.Tutorial.Localization
{
    /// <summary>
    /// Simple key table for the bootstrap tutorial scene. Replace with a
    /// shared localization service when the full pipeline is ready.
    /// </summary>
    public sealed class TutorialLocalizationProvider : ILocalizedTextProvider
    {
        private readonly Dictionary<string, string> _entries;

        public TutorialLocalizationProvider()
        {
            _entries = CreateDefaultEntries();
        }

        /// <inheritdoc />
        public string GetText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return _entries.TryGetValue(key, out string text) ? text : key;
        }

        private static Dictionary<string, string> CreateDefaultEntries()
        {
            return new Dictionary<string, string>
            {
                ["tutorial.intro.title"] = "ยินดีต้อนรับสู่ A-MATH",
                ["tutorial.intro.step1.objective"] = "ทำความรู้จักหน้าจอฝึกหัด",
                ["tutorial.intro.step1.dialogue"] = "นี่คือโหมดฝึกหัด — คุณจะเห็นเป้าหมาย ความคืบหน้า และคำใบ้ที่นี่",
                ["tutorial.intro.step1.hint"] = "อ่านเป้าหมายด้านบน แล้วกด \"ดำเนินการต่อ\" เมื่อพร้อม",
                ["tutorial.intro.step2.objective"] = "สังเกตพื้นที่กระดานตัวอย่าง",
                ["tutorial.intro.step2.dialogue"] = "กระดานจริงจะอยู่ตรงนี้ ตอนนี้เราไฮไลต์พื้นที่เพื่อให้เห็นชัด",
                ["tutorial.intro.step2.hint"] = "กด \"ดำเนินการต่อ\" เมื่อดูครบแล้ว",
                ["tutorial.intro.step3.objective"] = "จบบทนำ",
                ["tutorial.intro.step3.dialogue"] = "เยี่ยมมาก! ความคืบหน้าจะถูกบันทึกอัตโนมัติ — เปิดใหม่แล้วเล่นต่อได้",
            };
        }
    }
}
