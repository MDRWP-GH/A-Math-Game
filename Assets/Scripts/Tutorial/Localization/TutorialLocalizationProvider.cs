using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Settings;

namespace AMath.Tutorial.Localization
{
    /// <summary>
    /// Key table for the bootstrap tutorial scene. Language follows
    /// <see cref="GameSettings.LanguageIndex"/> (0 = English, 1 = Thai) so the
    /// tutorial matches the language chosen on the settings screen.
    /// </summary>
    public sealed class TutorialLocalizationProvider : ILocalizedTextProvider
    {
        private readonly Dictionary<string, string> _th;
        private readonly Dictionary<string, string> _en;

        public TutorialLocalizationProvider()
        {
            _th = CreateThaiEntries();
            _en = CreateEnglishEntries();
        }

        /// <inheritdoc />
        public string GetText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            Dictionary<string, string> table = GameSettings.LanguageIndex == 1 ? _th : _en;
            return table.TryGetValue(key, out string text) ? text : key;
        }

        private static Dictionary<string, string> CreateThaiEntries()
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
                ["tutorial.ui.title"] = "บทฝึกหัด A-MATH",
                ["tutorial.ui.board_area"] = "พื้นที่กระดานตัวอย่าง",
                ["tutorial.ui.continue"] = "ดำเนินการต่อ",
                ["tutorial.ui.replay"] = "เล่นขั้นนี้ใหม่",
                ["tutorial.ui.skip"] = "ข้ามบทฝึก",
                ["tutorial.ui.back_menu"] = "กลับเมนูหลัก",
                ["tutorial.ui.ask_ai"] = "ถาม AI",
                ["tutorial.ui.finished"] = "จบบทฝึกแล้ว — กด \"กลับเมนูหลัก\" เพื่อออก",
            };
        }

        private static Dictionary<string, string> CreateEnglishEntries()
        {
            return new Dictionary<string, string>
            {
                ["tutorial.intro.title"] = "Welcome to A-MATH",
                ["tutorial.intro.step1.objective"] = "Get to know the tutorial screen",
                ["tutorial.intro.step1.dialogue"] = "This is the tutorial mode — your objective, progress and hints appear here.",
                ["tutorial.intro.step1.hint"] = "Read the objective above, then press \"Continue\" when ready.",
                ["tutorial.intro.step2.objective"] = "Look at the demo board area",
                ["tutorial.intro.step2.dialogue"] = "The real board will live here. We highlight the area so it is easy to spot.",
                ["tutorial.intro.step2.hint"] = "Press \"Continue\" once you have had a look.",
                ["tutorial.intro.step3.objective"] = "Finish the introduction",
                ["tutorial.intro.step3.dialogue"] = "Great job! Progress is saved automatically — come back any time to continue.",
                ["tutorial.ui.title"] = "A-MATH Tutorial",
                ["tutorial.ui.board_area"] = "Demo board area",
                ["tutorial.ui.continue"] = "Continue",
                ["tutorial.ui.replay"] = "Replay Step",
                ["tutorial.ui.skip"] = "Skip Tutorial",
                ["tutorial.ui.back_menu"] = "Back to Menu",
                ["tutorial.ui.ask_ai"] = "Ask AI",
                ["tutorial.ui.finished"] = "Tutorial complete — press \"Back to Menu\" to leave.",
            };
        }
    }
}
