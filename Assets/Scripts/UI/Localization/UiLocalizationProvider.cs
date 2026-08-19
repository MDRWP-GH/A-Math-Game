using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Settings;

namespace AMath.UI.Localization
{
    /// <summary>
    /// Shared UI strings for the whole game: main menu, settings, how-to-play,
    /// lobby, match, pause, results and recovery overlays.
    /// Language follows <see cref="GameSettings.LanguageIndex"/> (0 = English, 1 = Thai),
    /// so a change on the settings screen applies everywhere immediately.
    /// </summary>
    public sealed class UiLocalizationProvider : ILocalizedTextProvider
    {
        /// <summary>Single shared instance — the tables are immutable, so every screen can reuse it.</summary>
        public static UiLocalizationProvider Shared { get; } = new UiLocalizationProvider();

        private readonly Dictionary<string, string> _th;
        private readonly Dictionary<string, string> _en;

        public UiLocalizationProvider()
        {
            _th = BuildThai();
            _en = BuildEnglish();
        }

        /// <inheritdoc />
        public string GetText(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            Dictionary<string, string> table = GameSettings.LanguageIndex == 1 ? _th : _en;
            return table.TryGetValue(key, out string text) ? text : key;
        }

        private static Dictionary<string, string> BuildThai() => new()
        {
            // Main menu
            ["ui.menu.title"] = "A-MATH The Project",
            ["ui.menu.subtitle"] = "สนุกกับการคิดเลข ทุกวัน",
            ["ui.menu.prompt"] = "พร้อมเริ่มฝึกแล้วหรือยัง?",
            ["ui.menu.start"] = "เริ่ม",
            ["ui.menu.tutorial"] = "ฝึกหัด",
            ["ui.menu.help"] = "วิธีเล่น",
            ["ui.menu.settings"] = "ตั้งค่า",
            ["ui.menu.quit"] = "ออก",
            ["ui.menu.status_hint"] = "เลือกเมนูเพื่อเริ่มต้น",
            ["ui.menu.opening_play"] = "กำลังเปิดโหมดเล่น…",
            ["ui.menu.no_tutorial"] = "ไม่พบฉากฝึกหัด — ตรวจสอบ Build Settings",

            // Settings
            ["ui.settings.title"] = "ตั้งค่า",
            ["ui.settings.tab_general"] = "ทั่วไป",
            ["ui.settings.tab_display"] = "การแสดงผล",
            ["ui.settings.tab_audio"] = "เสียง",
            ["ui.settings.tab_exit"] = "ออก",
            ["ui.settings.player_name"] = "ชื่อผู้เล่น",
            ["ui.settings.language"] = "ภาษา",
            ["ui.settings.view_mode"] = "โหมดหน้าจอ",
            ["ui.settings.resolution"] = "ความละเอียด",
            ["ui.settings.gui_scale"] = "ขนาด GUI",
            ["ui.settings.sfx"] = "เสียงเอฟเฟกต์",
            ["ui.settings.music"] = "เสียงเพลง",
            ["ui.settings.view_window"] = "หน้าต่าง",
            ["ui.settings.view_fullscreen"] = "เต็มจอ",
            ["ui.settings.view_borderless"] = "เต็มจอไร้ขอบ",
            ["ui.settings.hint"] = "Esc / B  •  กลับเมนู",
            ["ui.settings.name_placeholder"] = "ชื่อของคุณ",

            // How to play
            ["ui.help.title"] = "วิธีเล่น",
            ["ui.help.close"] = "ออก",
            ["ui.help.p1.heading"] = "แนะนำเกม",
            ["ui.help.p1.body"] =
                "A-Math เป็นเกมกระดานแนวคณิตศาสตร์ คล้าย crossword puzzle " +
                "ผู้เล่นต้องสร้างสมการคณิตศาสตร์ที่ถูกต้องบนตาราง โดยใช้ไทล์ตัวเลขและเครื่องหมาย\n\n" +
                "เป้าหมายคือสะสมคะแนนให้สูงที่สุด ด้วยไทล์แต้มสูงและการวางบนช่องโบนัส",
            ["ui.help.p2.heading"] = "อุปกรณ์ในเกม",
            ["ui.help.p2.body"] =
                "กระดาน: ตารางมาตรฐาน 15 × 15 ช่อง มีทั้งช่องธรรมดาและช่องคะแนนพรีเมียม " +
                "(สีต่างกันเพื่อบอกตัวคูณคะแนน)\n\n" +
                "ไทล์: ชุดไทล์ 100 ชิ้น ประกอบด้วย\n" +
                "• ตัวเลข: จำนวนเต็ม 0 ถึง 20\n" +
                "• เครื่องหมาย: บวก [+] ลบ [-] คูณ [×] และหาร [÷]\n" +
                "• เครื่องหมายเท่ากับ: [=]\n" +
                "• ไทล์ว่าง [ ]: ไวล์ดการ์ดแทนตัวเลขหรือเครื่องหมายใดก็ได้\n\n" +
                "ชั้นวาง: ผู้เล่นถือไทล์ได้ครั้งละ 8 ชิ้น",
            ["ui.help.p3.heading"] = "ระบบคะแนน",
            ["ui.help.p3.body"] =
                "คะแนนแต่ละตา คือผลรวมแต้มหน้าไทล์ทั้งหมดที่ใช้ในสมการที่สร้างหรือแก้ใหม่ " +
                "บวกโบนัสจากช่องพรีเมียม\n\n" +
                "• แดง (Triple Equation Score): คูณคะแนนทั้งสมการ ×3\n" +
                "• เหลือง (Double Equation Score): คูณคะแนนทั้งสมการ ×2\n" +
                "• ฟ้า (Triple Piece Score): คูณแต้มไทล์ที่วางบนช่องนี้ ×3\n" +
                "• ส้ม (Double Piece Score): คูณแต้มไทล์ที่วางบนช่องนี้ ×2\n\n" +
                "โบนัสชิ้นไทล์ใช้เฉพาะไทล์ที่วางในตานี้ และไทล์ร่วมนับได้ทุกสมการที่เกี่ยวข้อง\n" +
                "ใช้ไทล์ครบ 8 ชิ้นในตาเดียว รับโบนัสพิเศษ +40",
            ["ui.help.p4.heading"] = "วิธีเล่นในตาของคุณ",
            ["ui.help.p4.body"] =
                "1. เลือกไทล์จากชั้นวาง แล้วแตะช่องบนกระดานเพื่อวางแบบร่าง\n" +
                "2. ไทล์ว่าง ต้องเลือกค่าก่อนยืนยัน\n" +
                "3. ดูคะแนนตัวอย่างด้านข้าง แล้วกดยืนยันการวาง\n" +
                "4. ไม่มีตาเดิน? กดข้ามตา หรือแลกไทล์ที่เลือก\n" +
                "5. แต่ละตามีเวลาจำกัด 60 วินาที\n" +
                "6. สมการแรกต้องวางผ่านช่องกลางกระดาน",

            // Play flow
            ["ui.play.start"] = "เริ่มเกม",
            ["ui.play.browser_title"] = "ห้องเล่น",
            ["ui.play.create"] = "สร้างห้อง",
            ["ui.play.join"] = "เข้าห้อง",
            ["ui.play.join_code"] = "เข้าด้วยรหัส",
            ["ui.play.back"] = "กลับ",
            ["ui.play.searching"] = "กำลังค้นหาห้องในเครือข่าย…",
            ["ui.play.connecting"] = "กำลังเชื่อมต่อ…",
            ["ui.play.no_rooms"] = "ยังไม่พบห้อง",
            ["ui.play.room_name"] = "ชื่อห้อง",
            ["ui.play.room_code"] = "รหัสห้อง",
            ["ui.play.lobby_title"] = "ห้องรอ",
            ["ui.play.start_match"] = "เริ่มแมตช์",
            ["ui.play.leave"] = "ออกจากห้อง",
            ["ui.play.waiting_host"] = "รอเจ้าของห้องเริ่มเกม…",
            ["ui.play.members"] = "ผู้เล่นในห้อง",
            ["ui.play.you_badge"] = "คุณ",
            ["ui.play.host_badge"] = "โฮสต์",
            ["ui.play.err_create"] = "สร้างห้องไม่สำเร็จ",
            ["ui.play.err_join"] = "เข้าห้องไม่สำเร็จ",
            ["ui.play.err_not_joinable"] = "ห้องนี้เข้าไม่ได้ในขณะนี้",

            // Match HUD
            ["ui.match.confirm"] = "ยืนยันการวาง",
            ["ui.match.clear"] = "ล้างแบบร่าง",
            ["ui.match.pass"] = "ข้ามตา",
            ["ui.match.exchange"] = "แลกไทล์ที่เลือก",
            ["ui.match.bag"] = "ถุง",
            ["ui.match.turn"] = "ตา",
            ["ui.match.time"] = "เวลา",
            ["ui.match.you"] = "คุณ",
            ["ui.match.menu"] = "เมนู",
            ["ui.match.preview_ok"] = "แบบร่างใช้ได้ คะแนนประมาณ",
            ["ui.match.preview_bad"] = "แบบร่างยังไม่ถูกต้อง",
            ["ui.match.tile_points_hint"] = "ตัวเลขใต้สัญลักษณ์ = แต้มของไทล์ตามกติกา A-Math",
            ["ui.match.premium_legend"] = "2T/3T = คูณไทล์   2E/3E = คูณสมการ",
            ["ui.match.declare"] = "เลือกค่าไทล์ยืดหยุ่น",
            ["ui.match.your_turn"] = "ถึงตาคุณ",
            ["ui.match.wait_turn"] = "รอตาอื่น",
            ["ui.match.exchange_hint"] = "แตะไทล์ที่จะแลก แล้วกด “แลกไทล์ที่เลือก” อีกครั้ง",
            ["ui.match.idle_hint"] = "เลือกไทล์จากชั้นวาง แล้วแตะช่องบนกระดาน\nระบบจะตรวจสมการและคิดคะแนนให้อัตโนมัติ",

            // Pause
            ["ui.pause.title"] = "พักเกม",
            ["ui.pause.resume"] = "กลับสู่เกม",
            ["ui.pause.settings"] = "ตั้งค่า",
            ["ui.pause.leave"] = "ออกจากห้อง",

            // Results
            ["ui.result.title"] = "ผลการแข่งขัน",
            ["ui.result.rematch"] = "เล่นอีกครั้ง",
            ["ui.result.leave"] = "กลับเมนู",
            ["ui.result.winner"] = "ผู้ชนะ",
            ["ui.result.standings"] = "ตารางคะแนน",

            // Recovery
            ["ui.recovery.title"] = "การเชื่อมต่อขาดหาย",
            ["ui.recovery.wait"] = "รอต่อ",
            ["ui.recovery.end"] = "จบจากเซฟสำรอง",
            ["ui.recovery.grace"] = "รอเจ้าบ้านกลับมา…",
            ["ui.recovery.search"] = "กำลังค้นหาห้องในเครือข่าย…",
            ["ui.recovery.promote"] = "กำลังรับเป็นเจ้าบ้านใหม่…",
            ["ui.recovery.reconnect"] = "พบห้องแล้ว กำลังเชื่อมใหม่…",
            ["ui.recovery.recovered"] = "เชื่อมต่อสำเร็จ!",
        };

        private static Dictionary<string, string> BuildEnglish() => new()
        {
            // Main menu
            ["ui.menu.title"] = "A-MATH The Project",
            ["ui.menu.subtitle"] = "Fun with numbers, every day",
            ["ui.menu.prompt"] = "Ready to play?",
            ["ui.menu.start"] = "Start",
            ["ui.menu.tutorial"] = "Tutorial",
            ["ui.menu.help"] = "How To Play",
            ["ui.menu.settings"] = "Settings",
            ["ui.menu.quit"] = "Exit",
            ["ui.menu.status_hint"] = "Pick an option to begin.",
            ["ui.menu.opening_play"] = "Opening play mode…",
            ["ui.menu.no_tutorial"] = "Tutorial scene missing — check Build Settings",

            // Settings
            ["ui.settings.title"] = "Settings",
            ["ui.settings.tab_general"] = "General",
            ["ui.settings.tab_display"] = "Display",
            ["ui.settings.tab_audio"] = "Audio",
            ["ui.settings.tab_exit"] = "Exit",
            ["ui.settings.player_name"] = "Player Name",
            ["ui.settings.language"] = "Language",
            ["ui.settings.view_mode"] = "View Mode",
            ["ui.settings.resolution"] = "Resolution",
            ["ui.settings.gui_scale"] = "GUI Scale",
            ["ui.settings.sfx"] = "Sound Effects",
            ["ui.settings.music"] = "Music Volume",
            ["ui.settings.view_window"] = "Window",
            ["ui.settings.view_fullscreen"] = "Full Screen",
            ["ui.settings.view_borderless"] = "Borderless",
            ["ui.settings.hint"] = "Esc / B  •  back to menu",
            ["ui.settings.name_placeholder"] = "Your name",

            // How to play
            ["ui.help.title"] = "How To Play",
            ["ui.help.close"] = "Exit",
            ["ui.help.p1.heading"] = "Introduction",
            ["ui.help.p1.body"] =
                "A-Math is a mathematics-based board game analogous to a crossword puzzle. " +
                "Players are required to form valid mathematical equations on a grid using tiles " +
                "representing numbers and mathematical operators. The objective is to accumulate " +
                "the highest score by utilizing high-value tiles and placing them on premium scoring squares.",
            ["ui.help.p2.heading"] = "Equipment",
            ["ui.help.p2.body"] =
                "The Board: A standard grid consisting of 15 x 15 squares. The board includes standard " +
                "squares and premium scoring squares (colored differently to indicate score multipliers).\n\n" +
                "The Tiles: A set of 100 tiles comprising:\n" +
                "• Numbers: Integers from 0 to 20.\n" +
                "• Operators: Addition [+], Subtraction [-], Multiplication [×], and Division [÷].\n" +
                "• Equality Sign: Equal to [=].\n" +
                "• Blank Tiles [ ]: Wildcards that can represent any number or operator.\n\n" +
                "Rack: each player holds 8 tiles at a time.",
            ["ui.help.p3.heading"] = "Scoring System",
            ["ui.help.p3.body"] =
                "The score for each turn is the sum of the face values of all tiles used in the newly " +
                "formed or modified equations, plus bonuses from premium squares.\n\n" +
                "• Red (Triple Equation Score): Multiplies the total score of the entire equation by 3.\n" +
                "• Yellow (Double Equation Score): Multiplies the total score of the entire equation by 2.\n" +
                "• Blue (Triple Piece Score): Multiplies the value of the individual tile placed on this square by 3.\n" +
                "• Orange (Double Piece Score): Multiplies the value of the individual tile placed on this square by 2.\n\n" +
                "Tile premiums apply only to tiles placed this turn; shared tiles score in every equation.\n" +
                "Using all 8 rack tiles in one turn awards a +40 bonus.",
            ["ui.help.p4.heading"] = "Your Turn",
            ["ui.help.p4.body"] =
                "1. Select rack tiles and tap board cells to draft a placement.\n" +
                "2. Declare blank tiles before confirming.\n" +
                "3. Check the score preview, then press Confirm Place.\n" +
                "4. No move? Press Pass or Exchange Selected.\n" +
                "5. Each turn is limited to 60 seconds.\n" +
                "6. The first equation must cover the center square.",

            // Play flow
            ["ui.play.start"] = "Play",
            ["ui.play.browser_title"] = "Multiplayer Rooms",
            ["ui.play.create"] = "Create Room",
            ["ui.play.join"] = "Join",
            ["ui.play.join_code"] = "Join by Code",
            ["ui.play.back"] = "Back",
            ["ui.play.searching"] = "Searching the LAN for rooms…",
            ["ui.play.connecting"] = "Connecting…",
            ["ui.play.no_rooms"] = "No rooms found yet",
            ["ui.play.room_name"] = "Room name",
            ["ui.play.room_code"] = "Room code",
            ["ui.play.lobby_title"] = "Lobby",
            ["ui.play.start_match"] = "Start Match",
            ["ui.play.leave"] = "Leave Room",
            ["ui.play.waiting_host"] = "Waiting for the host to start…",
            ["ui.play.members"] = "Players in room",
            ["ui.play.you_badge"] = "You",
            ["ui.play.host_badge"] = "Host",
            ["ui.play.err_create"] = "Could not create the room",
            ["ui.play.err_join"] = "Could not join the room",
            ["ui.play.err_not_joinable"] = "That room cannot be joined right now",

            // Match HUD
            ["ui.match.confirm"] = "Confirm Place",
            ["ui.match.clear"] = "Clear Draft",
            ["ui.match.pass"] = "Pass",
            ["ui.match.exchange"] = "Exchange Selected",
            ["ui.match.bag"] = "Bag",
            ["ui.match.turn"] = "Turn",
            ["ui.match.time"] = "Time",
            ["ui.match.you"] = "You",
            ["ui.match.menu"] = "Menu",
            ["ui.match.preview_ok"] = "Draft OK — estimated score",
            ["ui.match.preview_bad"] = "Draft is not valid yet",
            ["ui.match.tile_points_hint"] = "Number under the symbol = official A-Math tile points",
            ["ui.match.premium_legend"] = "2T/3T = tile multiplier   2E/3E = equation multiplier",
            ["ui.match.declare"] = "Declare flexible tile",
            ["ui.match.your_turn"] = "Your turn",
            ["ui.match.wait_turn"] = "Waiting for another player",
            ["ui.match.exchange_hint"] = "Tap the tiles to trade in, then press Exchange again",
            ["ui.match.idle_hint"] = "Pick a tile from your rack, then tap a board cell.\nEquations are validated and scored automatically.",

            // Pause
            ["ui.pause.title"] = "Paused",
            ["ui.pause.resume"] = "Back To Game",
            ["ui.pause.settings"] = "Settings",
            ["ui.pause.leave"] = "Leave Room",

            // Results
            ["ui.result.title"] = "Match Results",
            ["ui.result.rematch"] = "Rematch",
            ["ui.result.leave"] = "Back to Menu",
            ["ui.result.winner"] = "Winner",
            ["ui.result.standings"] = "Standings",

            // Recovery
            ["ui.recovery.title"] = "Connection Lost",
            ["ui.recovery.wait"] = "Keep Waiting",
            ["ui.recovery.end"] = "End From Backup Save",
            ["ui.recovery.grace"] = "Waiting for the host to return…",
            ["ui.recovery.search"] = "Searching the LAN for the room…",
            ["ui.recovery.promote"] = "Taking over as the new host…",
            ["ui.recovery.reconnect"] = "Room found — reconnecting…",
            ["ui.recovery.recovered"] = "Reconnected!",
        };
    }
}
