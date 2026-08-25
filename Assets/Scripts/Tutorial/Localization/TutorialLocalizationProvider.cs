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
                ["tutorial.intro.step1.dialogue"] = "นี่คือโหมดฝึกหัด — คุณจะเดินหมากเอง แต่ต้องวางตามสคริปต์ของบทเรียน ฝ่ายตรงข้ามเป็นบอทที่เดินตามสคริปต์เช่นกัน ไม่มี AI เลือกตาให้",
                ["tutorial.intro.step1.hint"] = "อ่านเป้าหมายด้านบน แล้วกด \"ดำเนินการต่อ\" เมื่อพร้อม",
                ["tutorial.intro.step2.objective"] = "ดูกระดานและแร็คของตัวเอง",
                ["tutorial.intro.step2.dialogue"] = "กระดานอยู่ตรงกลาง หมากของคุณอยู่แถวล่าง ช่อง ★ คือช่องกลางที่สมการแรกต้องครอบ",
                ["tutorial.intro.step2.hint"] = "กด \"ดำเนินการต่อ\" เมื่อดูกระดานครบแล้ว",
                ["tutorial.intro.step3.objective"] = "วางสมการ 1 + 2 = 3 ผ่านช่องกลาง",
                ["tutorial.intro.step3.dialogue"] = "เลือกหมากจากแร็คแล้ววางบนช่องสีทองตามลำดับใดก็ได้ ให้ได้ 1 + 2 = 3 ผ่านช่อง ★ จากนั้นกดยืนยัน — วางนอกสคริปต์ไม่ได้",
                ["tutorial.intro.step3.hint"] = "เลือกหมากที่ไฮไลต์ แล้ววางบนช่องสีทอง ครบห้าชิ้นแล้วกดยืนยัน",
                ["tutorial.intro.step4.objective"] = "ดูบอทวางตามสคริปต์",
                ["tutorial.intro.step4.dialogue"] = "บอทจะต่อจากเลข 2 ที่ช่องกลาง เป็น 2 + 2 = 4 ตามสคริปต์เท่านั้น — ไม่ได้คิดตาเอง",
                ["tutorial.intro.step4.hint"] = "รอสักครู่ บอทจะวางตามบทเรียน",
                ["tutorial.intro.step5.objective"] = "จบบทฝึก",
                ["tutorial.intro.step5.dialogue"] = "เยี่ยมมาก! คุณควบคุมหมากเองได้ แต่ต้องตามสคริปต์ ความคืบหน้าถูกบันทึกแล้ว",
                ["tutorial.ui.title"] = "บทฝึกหัด A-MATH",
                ["tutorial.ui.continue"] = "ดำเนินการต่อ",
                ["tutorial.ui.replay"] = "เล่นขั้นนี้ใหม่",
                ["tutorial.ui.skip"] = "ข้ามบทฝึก",
                ["tutorial.ui.back_menu"] = "กลับเมนูหลัก",
                ["tutorial.ui.finished"] = "จบบทฝึกแล้ว — กด \"กลับเมนูหลัก\" เพื่อออก",
                ["tutorial.ui.confirm"] = "ยืนยัน",
                ["tutorial.ui.clear"] = "ล้าง",
                ["tutorial.ui.your_turn"] = "ตาของคุณ — วางตามช่องที่ไฮไลต์",
                ["tutorial.ui.wait_script"] = "รอสคริปต์ของเกม",
                ["tutorial.ui.you"] = "คุณ",
                ["tutorial.ui.bot"] = "บอท",
                ["tutorial.error.not_your_step"] = "ตอนนี้ยังไม่ถึงตาให้คุณวางตามสคริปต์",
                ["tutorial.error.off_script_cell"] = "วางได้เฉพาะช่องที่ไฮไลต์ตามสคริปต์",
                ["tutorial.error.off_script_tile"] = "ต้องใช้หมากตามสคริปต์สำหรับช่องนี้",
                ["tutorial.error.off_script_confirm"] = "วางให้ครบสมการตามสคริปต์ก่อนยืนยัน",
                ["tutorial.error.pass_disabled"] = "โหมดฝึกหัดห้ามผ่านตา",
                ["tutorial.error.exchange_disabled"] = "โหมดฝึกหัดห้ามสลับหมาก",
            };
        }

        private static Dictionary<string, string> CreateEnglishEntries()
        {
            return new Dictionary<string, string>
            {
                ["tutorial.intro.title"] = "Welcome to A-MATH",
                ["tutorial.intro.step1.objective"] = "Get to know the tutorial screen",
                ["tutorial.intro.step1.dialogue"] = "This is tutorial mode — you place your own tiles, but only where the script says. The opponent is a bot that also follows the script. There is no AI choosing moves.",
                ["tutorial.intro.step1.hint"] = "Read the objective above, then press \"Continue\" when ready.",
                ["tutorial.intro.step2.objective"] = "Look at the board and your rack",
                ["tutorial.intro.step2.dialogue"] = "The board is in the centre and your tiles are along the bottom. The ★ cell is the centre square the first equation must cover.",
                ["tutorial.intro.step2.hint"] = "Press \"Continue\" once you have had a look.",
                ["tutorial.intro.step3.objective"] = "Place 1 + 2 = 3 through the centre",
                ["tutorial.intro.step3.dialogue"] = "Pick tiles from your rack and drop them on the gold cells, in any order, to make 1 + 2 = 3 through ★. Then confirm. Off-script placements are rejected.",
                ["tutorial.intro.step3.hint"] = "Select a highlighted tile, place it on a gold cell, then confirm when all five are down.",
                ["tutorial.intro.step4.objective"] = "Watch the bot follow the script",
                ["tutorial.intro.step4.dialogue"] = "The bot extends the centre 2 into 2 + 2 = 4. It only plays the authored move — it does not search for one.",
                ["tutorial.intro.step4.hint"] = "Wait a moment — the bot will play the scripted line.",
                ["tutorial.intro.step5.objective"] = "Finish the tutorial",
                ["tutorial.intro.step5.dialogue"] = "Great job! You controlled your own tiles, but only along the script. Progress is saved.",
                ["tutorial.ui.title"] = "A-MATH Tutorial",
                ["tutorial.ui.continue"] = "Continue",
                ["tutorial.ui.replay"] = "Replay Step",
                ["tutorial.ui.skip"] = "Skip Tutorial",
                ["tutorial.ui.back_menu"] = "Back to Menu",
                ["tutorial.ui.finished"] = "Tutorial complete — press \"Back to Menu\" to leave.",
                ["tutorial.ui.confirm"] = "Confirm",
                ["tutorial.ui.clear"] = "Clear",
                ["tutorial.ui.your_turn"] = "Your turn — place on the highlighted cells",
                ["tutorial.ui.wait_script"] = "Waiting for the scripted turn",
                ["tutorial.ui.you"] = "you",
                ["tutorial.ui.bot"] = "bot",
                ["tutorial.error.not_your_step"] = "It is not your scripted turn yet.",
                ["tutorial.error.off_script_cell"] = "Place only on the highlighted scripted cells.",
                ["tutorial.error.off_script_tile"] = "Use the scripted tile for that square.",
                ["tutorial.error.off_script_confirm"] = "Finish the scripted equation before confirming.",
                ["tutorial.error.pass_disabled"] = "Passing is disabled in the tutorial.",
                ["tutorial.error.exchange_disabled"] = "Exchanging is disabled in the tutorial.",
            };
        }
    }
}
