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
            ["ui.menu.history"] = "ประวัติการแข่งขัน",

            // Scene loading
            ["ui.loading.startup"] = "กำลังเริ่มเกม…",
            ["ui.loading.account"] = "กำลังเตรียมบัญชีผู้เล่น…",
            ["ui.loading.tutorial"] = "กำลังเปิดบทสอน…",
            ["ui.loading.menu"] = "กำลังกลับสู่เมนูหลัก…",
            ["ui.loading.scene"] = "กำลังโหลด…",
            ["ui.loading.ready"] = "พร้อมแล้ว",

            // Local accounts
            ["ui.account.login_title"] = "เข้าสู่ระบบ",
            ["ui.account.register_title"] = "สร้างบัญชีใหม่",
            ["ui.account.subtitle"] = "เลือกบัญชีเพื่อโหลดเซฟของคุณบนเครื่องนี้",
            ["ui.account.login"] = "เข้าสู่ระบบ",
            ["ui.account.register"] = "สมัครบัญชี",
            ["ui.account.username"] = "ชื่อผู้ใช้",
            ["ui.account.password"] = "รหัสผ่าน",
            ["ui.account.username_hint"] = "ชื่อผู้ใช้",
            ["ui.account.password_hint"] = "รหัสผ่านอย่างน้อย 4 ตัว",
            ["ui.account.save_hint"] = "เซฟแยกตามบัญชี • เริ่มใหม่ให้สร้างบัญชีใหม่\nย้ายเครื่อง: ปิดเกม คัดลอกโฟลเดอร์บัญชีใน save/users แล้วล็อกอิน",
            ["ui.account.confirm_title"] = "ยืนยันการสร้างบัญชี",
            ["ui.account.confirm_body"] = "สร้างบัญชี “{0}” และเริ่มเซฟใหม่ใช่หรือไม่?",
            ["ui.account.confirm"] = "ตกลง",
            ["ui.account.cancel"] = "ยกเลิก",
            ["ui.account.logout"] = "ออกจาก\nระบบ",
            ["ui.account.signed_in"] = "บัญชี: {0}",
            ["ui.account.version"] = "v.{0}",
            ["ui.account.err_username_required"] = "กรุณากรอกชื่อผู้ใช้",
            ["ui.account.err_username_long"] = "ชื่อผู้ใช้ต้องไม่เกิน 24 ตัวอักษร",
            ["ui.account.err_password_required"] = "กรุณากรอกรหัสผ่าน",
            ["ui.account.err_password_short"] = "รหัสผ่านต้องมีอย่างน้อย 4 ตัวอักษร",
            ["ui.account.err_username_taken"] = "ชื่อผู้ใช้นี้ถูกใช้แล้ว",
            ["ui.account.err_invalid"] = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง",
            ["ui.account.err_storage"] = "เขียนโฟลเดอร์ save ข้างเกมไม่ได้ กรุณาย้ายเกมไปยังตำแหน่งที่เขียนได้ แล้วเปิดใหม่",
            ["ui.account.err_conflict"] = "พบชื่อผู้ใช้หรือรหัสบัญชีซ้ำใน save/users กรุณาสำรองข้อมูลและแก้ความขัดแย้งก่อน",
            ["ui.account.err_migration"] = "ย้ายข้อมูลเก่าค้างอยู่ ต้นฉบับใน LocalLow ยังอยู่ กรุณาสำรอง save และตรวจสอบก่อนลองอีกครั้ง",
            ["ui.account.err_corrupt"] = "ข้อมูลบัญชีเสียหาย กรุณาสำรองข้อมูลและตรวจสอบไฟล์เกม",
            ["ui.account.err_session"] = "เซสชันหมดอายุ กรุณาเข้าสู่ระบบอีกครั้ง",

            // History
            ["ui.history.title"] = "ประวัติการแข่งขัน",
            ["ui.history.empty"] = "ยังไม่มีประวัติ — เล่นจบแมตช์แล้วจะบันทึกที่นี่",
            ["ui.history.close"] = "ปิด",
            ["ui.history.format_individual"] = "เดี่ยว",
            ["ui.history.format_team"] = "ทีม",
            ["ui.history.duration_min"] = "{0} น.{1} ว.",
            ["ui.history.duration_sec"] = "{0} ว.",
            ["ui.history.player_score"] = "{0}: {1}",
            ["ui.history.win"] = "ชนะ",
            ["ui.history.lose"] = "แพ้",
            ["ui.history.prev"] = "ก่อนหน้า",
            ["ui.history.next"] = "ถัดไป",
            ["ui.history.replay_title"] = "เทิร์น {0}/{1} — {2}",
            ["ui.history.replay_start"] = "ก่อนเริ่มแมตช์",
            ["ui.history.replay_turn"] = "เทิร์น {0}: ผู้เล่น {1} (+{2})",
            ["ui.history.replay_finished"] = "จบการแข่งขันแล้ว",
            ["ui.history.replay_error"] = "เปิด replay ไม่ได้",
            ["ui.history.unknown_room"] = "ห้องไม่ระบุชื่อ",

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
            ["ui.player_name.err_required"] = "กรุณากรอกชื่อผู้เล่น",
            ["ui.player_name.err_too_long"] = "ชื่อผู้เล่นต้องไม่เกิน 24 ตัวอักษร",
            ["ui.player_name.err_unsupported"] = "ใช้ได้เฉพาะภาษาไทย อังกฤษ ตัวเลข เว้นวรรค ขีดกลาง และขีดล่าง",

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
                "5. โฮสต์เลือกเวลาต่อตาในห้องรอ: Rush 60 / Short 90 / Normal 120 / Long 180 วินาที\n" +
                "6. สมการแรกต้องวางผ่านช่องกลางกระดาน",

            // Play flow
            ["ui.play.start"] = "เริ่มเกม",
            ["ui.play.subtitle"] = "เลือกวิธีเชื่อมต่อ แล้วเตรียมห้องแข่งขันของคุณ",
            ["ui.play.host"] = "สร้างห้องและเล่น",
            ["ui.play.host_description"] = "สร้างห้องใหม่บนเครือข่ายนี้ แล้วชวนเพื่อนด้วยรหัสห้อง",
            ["ui.play.join_description"] = "ค้นหาห้องที่เปิดอยู่ หรือกรอกรหัสเพื่อเข้าร่วมโดยตรง",
            ["ui.play.host_title"] = "สร้างห้องแข่งขัน",
            ["ui.play.host_setup_subtitle"] = "ตั้งชื่อห้องสั้น ๆ เพื่อให้เพื่อนหาเจอได้ง่าย",
            ["ui.play.play_action"] = "เล่น",
            ["ui.play.browser_title"] = "ค้นหาห้องแข่งขัน",
            ["ui.play.browser_subtitle"] = "ห้องบนเครือข่ายเดียวกันจะปรากฏในรายการโดยอัตโนมัติ",
            ["ui.play.create"] = "สร้างห้อง",
            ["ui.play.join"] = "เข้าร่วม",
            ["ui.play.join_code"] = "เข้าด้วยรหัส",
            ["ui.play.back"] = "กลับ",
            ["ui.play.searching"] = "กำลังค้นหาโฮสต์ในเครือข่าย…",
            ["ui.play.connecting"] = "กำลังเชื่อมต่อ…",
            ["ui.play.no_rooms"] = "ยังไม่พบห้อง",
            ["ui.play.discovery_failed"] = "เปิดพอร์ต UDP 47777 ไม่ได้ — ปิดเกมที่ค้างอยู่หรืออนุญาต A-Math ใน Windows Firewall แล้วลองใหม่",
            ["ui.play.room_name"] = "ชื่อห้อง",
            ["ui.play.player_name"] = "ชื่อผู้เล่นในเกม",
            ["ui.play.room_name_hint"] = "เว้นว่างได้ ระบบจะใช้ชื่อห้องเริ่มต้นให้โดยอัตโนมัติ",
            ["ui.play.room_capacity"] = "รองรับผู้เล่น 2–4 คน • เลือกแข่งเดี่ยวหรือแข่งทีมได้ในห้องรอ",
            ["ui.play.room_code"] = "รหัสห้อง",
            ["ui.play.available_rooms"] = "ห้องที่ค้นพบ",
            ["ui.play.manual_code"] = "มีรหัสห้องอยู่แล้ว?",
            ["ui.play.room_players"] = "รหัส {0}  •  ผู้เล่น {1}/{2}",
            ["ui.play.lobby_title"] = "ห้องรอการแข่งขัน",
            ["ui.play.lobby_subtitle"] = "ตรวจสมาชิก เลือกสีและรูปแบบให้พร้อมก่อนเริ่มแมตช์",
            ["ui.play.match_setup"] = "ตั้งค่าการแข่งขัน",
            ["ui.play.start_match"] = "เริ่มแมตช์",
            ["ui.play.leave"] = "ออกจากห้อง",
            ["ui.play.waiting_host"] = "รอเจ้าของห้องเริ่มเกม…",
            ["ui.play.members"] = "ผู้เล่นในห้อง",
            ["ui.play.waiting_player_slot"] = "กำลังรอผู้เล่น…",
            ["ui.play.you_badge"] = "คุณ",
            ["ui.play.host_badge"] = "โฮสต์",
            ["ui.play.team_badge"] = "ทีม",
            ["ui.play.format"] = "รูปแบบการแข่งขัน",
            ["ui.play.format_individual"] = "แข่งเดี่ยว",
            ["ui.play.format_team"] = "แข่งทีม",
            ["ui.play.turn_time"] = "เวลาต่อตา",
            ["ui.play.turn_rush"] = "Rush (60s)",
            ["ui.play.turn_short"] = "Short (90s)",
            ["ui.play.turn_normal"] = "Normal (120s)",
            ["ui.play.turn_long"] = "Long (180s)",
            ["ui.play.team_hint"] = "เลือกทีม 1 หรือ ทีม 2 — ต้องมี 2–4 คน และทั้งสองทีมมีสมาชิก",
            ["ui.play.pick_team"] = "เลือกทีม",
            ["ui.play.team_one"] = "ทีม 1",
            ["ui.play.team_two"] = "ทีม 2",
            ["ui.play.your_color"] = "สีของคุณ",
            ["ui.color.red"] = "แดง",
            ["ui.color.orange"] = "ส้ม",
            ["ui.color.yellow"] = "เหลือง",
            ["ui.color.dark_green"] = "เขียวเข้ม",
            ["ui.color.light_green"] = "เขียวอ่อน",
            ["ui.color.light_blue"] = "ฟ้า",
            ["ui.color.blue"] = "น้ำเงิน",
            ["ui.color.purple"] = "ม่วง",
            ["ui.color.pink"] = "ชมพู",
            ["ui.color.brown"] = "น้ำตาล",
            ["ui.color.black"] = "ดำ",
            ["ui.color.grey"] = "เทา",
            ["ui.color.teal"] = "ฟ้าอมเขียว",
            ["ui.play.err_create"] = "สร้างห้องไม่สำเร็จ",
            ["ui.play.err_join"] = "เข้าห้องไม่สำเร็จ",
            ["ui.play.err_already_session"] = "คุณอยู่ในห้องหรือกำลังเชื่อมต่ออยู่แล้ว",
            ["ui.play.err_invalid_room"] = "ข้อมูลห้องไม่ถูกต้องหรือหมดอายุแล้ว",
            ["ui.play.err_room_full"] = "ห้องนี้เต็มแล้ว",
            ["ui.play.err_match_started"] = "ห้องนี้เริ่มการแข่งขันไปแล้ว",
            ["ui.play.err_room_closed"] = "ห้องถูกปิดแล้วหรือโฮสต์ไม่พร้อมเชื่อมต่อ",
            ["ui.play.err_network"] = "ไม่พบเครือข่าย LAN ที่ใช้งานได้ กรุณาตรวจสายหรือ Wi-Fi แล้วลองใหม่",
            ["ui.play.err_transport"] = "เริ่มการเชื่อมต่อไม่สำเร็จ กรุณาตรวจพอร์ตและ Firewall แล้วลองใหม่",
            ["ui.play.err_not_joinable"] = "ห้องนี้เข้าไม่ได้ในขณะนี้",
            ["ui.play.err_code_format"] = "รูปแบบรหัสห้องไม่ถูกต้อง",
            ["ui.play.err_code_not_found"] = "ไม่พบห้องที่ใช้รหัสนี้ในเครือข่าย",
            ["ui.play.connection_lost"] = "การเชื่อมต่อขาดหาย — กลับมาที่หน้าค้นหาห้อง",
            ["ui.play.need_host"] = "เฉพาะเจ้าของห้องเท่านั้นที่เริ่มแมตช์ได้",
            ["ui.play.need_players"] = "รอผู้เล่นเข้าห้อง…",
            ["ui.play.need_two_players"] = "ต้องมีผู้เล่นอย่างน้อย 2 คน",
            ["ui.play.need_both_teams"] = "ทั้งสองทีมต้องมีสมาชิกอย่างน้อย 1 คน",

            // Match HUD
            ["ui.match.confirm"] = "ยืนยันการวาง",
            ["ui.match.clear"] = "ล้างแบบร่าง",
            ["ui.match.pass"] = "ข้ามตา",
            ["ui.match.exchange"] = "แลกไทล์ที่เลือก",
            ["ui.match.commands_open"] = "คำสั่ง  ▲",
            ["ui.match.commands_hide"] = "ซ่อนคำสั่ง  ▼",
            ["ui.match.bag"] = "ถุง",
            ["ui.match.score"] = "คะแนน :",
            ["ui.match.tile_preview_points"] = "เบี้ยนี้ {0} คะแนนเมื่อวางช่องปกติ",
            ["ui.match.turn"] = "ตา",
            ["ui.match.time"] = "เวลา",
            ["ui.match.you"] = "คุณ",
            ["ui.match.menu"] = "เมนู",
            ["ui.match.preview_ok"] = "แบบร่างใช้ได้ คะแนนประมาณ",
            ["ui.match.preview_bad"] = "แบบร่างยังไม่ถูกต้อง",
            ["ui.match.tile_points_hint"] = "ตัวเลขใต้สัญลักษณ์ = แต้มของไทล์ตามกติกา A-Math",
            ["ui.match.premium_legend"] = "x2/x3 PIECE = คูณไทล์   x2/x3 WORD = คูณสมการ",
            ["ui.match.declare"] = "เลือกค่าไทล์ยืดหยุ่น",
            ["ui.match.your_turn"] = "ถึงตาคุณ",
            ["ui.match.wait_turn"] = "รอตาอื่น",
            ["ui.match.waiting_players"] = "พักเกมชั่วคราว — รอผู้เล่นที่หลุดกลับเข้ามา",
            ["ui.match.waiting_players_countdown"] = "รอผู้เล่นที่หลุด… เล่นต่อโดยไม่รอในอีก {0} วิ",
            ["ui.match.exchange_hint"] = "แตะไทล์ที่จะแลก แล้วกด “แลกไทล์ที่เลือก” อีกครั้ง",
            ["ui.match.idle_hint"] = "เลือกไทล์จากชั้นวาง แล้วแตะช่องบนกระดาน\nระบบจะตรวจสมการและคิดคะแนนให้อัตโนมัติ",
            ["ui.match.ask_ai"] = "ถาม AI",

            // AI assistant
            ["ai.window.title"] = "ผู้ช่วย AI",
            ["ai.window.mode_rules"] = "กติกา",
            ["ai.window.mode_strategy"] = "กลยุทธ์",
            ["ai.window.send"] = "ถาม",
            ["ai.window.close"] = "ปิด",
            ["ai.window.busy"] = "กำลังคิด…",
            ["ai.window.placeholder"] = "ถามเรื่องกติกาได้เลย…",
            ["ai.error.mode_unavailable"] = "ตอนนี้ยังใช้ผู้ช่วย AI ไม่ได้",
            ["ai.error.empty_question"] = "พิมพ์คำถามก่อนกดถาม",
            ["ai.error.unsafe_response"] = "คำตอบถูกระงับเพราะไม่ผ่านการตรวจสอบความปลอดภัย",
            ["ai.error.request_failed"] = "ติดต่อผู้ช่วย AI ไม่สำเร็จ ลองใหม่อีกครั้ง",
            ["ai.error.mode_not_ready"] = "โหมดนี้ยังตอบไม่ได้ในช่วงนี้ของเกม",
            ["ai.strategy.header"] = "คำใบ้จากสถานะกระดาน (สคริปต์):",
            ["ai.strategy.no_tips"] =
                "ยังไม่มีคำใบ้พิเศษจากสถานะตอนนี้ — ลองหาสมการที่ทั้งสองข้างของ '=' เท่ากัน " +
                "และต่อกับกระดาน แล้วค่อยวิเคราะห์เองอีกครั้ง",
            ["ai.strategy.question_ignored"] =
                "คำถามของคุณถูกบันทึกไว้แล้ว แต่โหมดนี้ใช้การตรวจจับสถานะ ไม่ได้วิเคราะห์ข้อความอิสระ",
            ["ai.strategy.rejection"] =
                "การวางล่าสุดไม่ผ่าน: {0} ลองจัดสมการใหม่ให้ทั้งสองข้างของ '=' เท่ากัน และต่อกับกระดานที่มีอยู่",
            ["ai.strategy.first_move_center"] =
                "ตาแรกต้องวางสมการให้ครอบช่องกลางกระดาน และมีความยาวอย่างน้อย 3 ชิ้น เช่น 1+2=3",
            ["ai.strategy.missing_equals"] =
                "ในมือยังไม่มี '=' (หรือใบว่างที่จะใช้แทน) — ลองแลกไทล์ หรือรอจังหวะที่มี '=' ก่อนวางสมการยาว",
            ["ai.strategy.low_bag"] =
                "ถุงไทล์เหลือน้อยแล้ว — ระวังไทล์ติดมือตอนจบเกม จะถูกหักคะแนน และคนที่หมดมือก่อนจะได้โบนัสจากของคนอื่น",
            ["ai.strategy.few_numbers"] =
                "ตัวเลขในมือน้อย — แลกไทล์บางใบ หรือหาสมการสั้นๆ อย่าง ก=ก ถ้ามีเลขคู่และ '='",
            ["ai.strategy.equals_but_stuck"] =
                "มีวัตถุดิบพอสำหรับสมการแบบ ก+ข=ค — ลองวางต่อจากไทล์บนกระดานในแถวหรือคอลัมน์เดียว ให้ต่อเนื่องไม่มีช่องว่าง",

            // Pause
            ["ui.pause.title"] = "พักเกม",
            ["ui.pause.resume"] = "กลับสู่เกม",
            ["ui.pause.play_on"] = "เล่นต่อโดยไม่รอ",
            ["ui.pause.settings"] = "ตั้งค่า",
            ["ui.pause.leave"] = "ออกจากห้อง",

            // Results
            ["ui.result.title"] = "ผลการแข่งขัน",
            ["ui.result.rematch"] = "เล่นอีกครั้ง",
            ["ui.result.leave"] = "กลับเมนู",
            ["ui.result.winner"] = "ผู้ชนะ",
            ["ui.result.outcome"] = "ผลการแข่งขัน",
            ["ui.result.draw"] = "เสมอ",
            ["ui.result.standings"] = "ตารางคะแนน",
            ["ui.result.team_winner"] = "ทีมชนะ: ทีม {0}",
            ["ui.result.team_standings"] = "คะแนนรวมทีม",
            ["ui.result.duration_min"] = "เวลา {0} น.{1} ว.",
            ["ui.result.duration_sec"] = "เวลา {0} ว.",
            ["ui.history.draw"] = "เสมอ",
            ["ui.history.recovered"] = "กู้คืนประวัติจากข้อมูลสำรองแล้ว",
            ["ui.history.rebuilt"] = "สร้างรายการประวัติใหม่จากรีเพลย์แล้ว",
            ["ui.history.storage_unavailable"] = "ไม่สามารถเข้าถึงพื้นที่เก็บประวัติได้",

            // Recovery
            ["ui.recovery.title"] = "การเชื่อมต่อขาดหาย",
            ["ui.recovery.wait"] = "รอต่อ",
            ["ui.recovery.leave"] = "ออกจากห้อง",
            ["ui.recovery.end"] = "จบจากเซฟสำรอง",
            ["ui.recovery.grace"] = "กำลังเชื่อมต่อใหม่…",
            ["ui.recovery.search"] = "กำลังค้นหาห้องในเครือข่าย…",
            ["ui.recovery.promote"] = "กำลังเชื่อมต่อใหม่…",
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
            ["ui.menu.history"] = "Match History",

            // Scene loading
            ["ui.loading.startup"] = "Starting game…",
            ["ui.loading.account"] = "Preparing player account…",
            ["ui.loading.tutorial"] = "Opening tutorial…",
            ["ui.loading.menu"] = "Returning to main menu…",
            ["ui.loading.scene"] = "Loading…",
            ["ui.loading.ready"] = "Ready",

            // Local accounts
            ["ui.account.login_title"] = "Sign in",
            ["ui.account.register_title"] = "Create an account",
            ["ui.account.subtitle"] = "Choose an account to load your saves on this computer",
            ["ui.account.login"] = "Login",
            ["ui.account.register"] = "Register",
            ["ui.account.username"] = "Username",
            ["ui.account.password"] = "Password",
            ["ui.account.username_hint"] = "Username",
            ["ui.account.password_hint"] = "At least 4 characters",
            ["ui.account.save_hint"] = "Saves are per account; create another account for a fresh start.\nMoving PCs? Close the game, copy your save/users account folder, then sign in.",
            ["ui.account.confirm_title"] = "Confirm registration",
            ["ui.account.confirm_body"] = "Create “{0}” and begin a new save?",
            ["ui.account.confirm"] = "Confirm",
            ["ui.account.cancel"] = "Cancel",
            ["ui.account.logout"] = "Log\nout",
            ["ui.account.signed_in"] = "Account: {0}",
            ["ui.account.version"] = "v.{0}",
            ["ui.account.err_username_required"] = "Enter a username.",
            ["ui.account.err_username_long"] = "Username must be 24 characters or fewer.",
            ["ui.account.err_password_required"] = "Enter a password.",
            ["ui.account.err_password_short"] = "Password must contain at least 4 characters.",
            ["ui.account.err_username_taken"] = "That username is already in use.",
            ["ui.account.err_invalid"] = "Username or password is incorrect.",
            ["ui.account.err_storage"] = "Cannot write the save folder beside the game. Move the game to a writable location and restart.",
            ["ui.account.err_conflict"] = "Duplicate username or account ID in save/users. Back up the folders and resolve the conflict first.",
            ["ui.account.err_migration"] = "Legacy import was interrupted. The LocalLow original is intact; back up save and inspect it before retrying.",
            ["ui.account.err_corrupt"] = "Account data is damaged. Back it up and check the game files.",
            ["ui.account.err_session"] = "Your session expired. Please sign in again.",

            // History
            ["ui.history.title"] = "Match History",
            ["ui.history.empty"] = "No history yet — finish a match to archive it here",
            ["ui.history.close"] = "Close",
            ["ui.history.format_individual"] = "Individual",
            ["ui.history.format_team"] = "Team",
            ["ui.history.duration_min"] = "{0}m {1}s",
            ["ui.history.duration_sec"] = "{0}s",
            ["ui.history.player_score"] = "{0}: {1}",
            ["ui.history.win"] = "Win",
            ["ui.history.lose"] = "Loss",
            ["ui.history.prev"] = "Previous",
            ["ui.history.next"] = "Next",
            ["ui.history.replay_title"] = "Turn {0}/{1} — {2}",
            ["ui.history.replay_start"] = "Before the match started",
            ["ui.history.replay_turn"] = "Turn {0}: player {1} (+{2})",
            ["ui.history.replay_finished"] = "Match finished",
            ["ui.history.replay_error"] = "Could not open replay",
            ["ui.history.unknown_room"] = "Unnamed room",

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
            ["ui.player_name.err_required"] = "Enter an in-game player name",
            ["ui.player_name.err_too_long"] = "Player name must be 24 characters or fewer",
            ["ui.player_name.err_unsupported"] = "Use Thai or English letters, numbers, spaces, hyphens, or underscores only",

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
                "5. The host picks turn time in the lobby: Rush 60 / Short 90 / Normal 120 / Long 180 seconds.\n" +
                "6. The first equation must cover the center square.",

            // Play flow
            ["ui.play.start"] = "Play",
            ["ui.play.subtitle"] = "Choose how to connect, then prepare your match room",
            ["ui.play.host"] = "Host & Play",
            ["ui.play.host_description"] = "Create a room on this network and invite friends with its room code.",
            ["ui.play.join_description"] = "Browse open rooms or enter a code to join one directly.",
            ["ui.play.host_title"] = "Create a Match Room",
            ["ui.play.host_setup_subtitle"] = "Use a short room name so your friends can find it quickly",
            ["ui.play.play_action"] = "Play",
            ["ui.play.browser_title"] = "Find a Match Room",
            ["ui.play.browser_subtitle"] = "Rooms on the same network appear here automatically",
            ["ui.play.create"] = "Create Room",
            ["ui.play.join"] = "Join via LAN",
            ["ui.play.join_code"] = "Join by Code",
            ["ui.play.back"] = "Back",
            ["ui.play.searching"] = "Searching for Local Hosts…",
            ["ui.play.connecting"] = "Connecting…",
            ["ui.play.no_rooms"] = "No rooms found yet",
            ["ui.play.discovery_failed"] = "Could not open UDP port 47777 — close any stuck game instance or allow A-Math through Windows Firewall, then try again",
            ["ui.play.room_name"] = "Room name",
            ["ui.play.player_name"] = "In-game player name",
            ["ui.play.room_name_hint"] = "You can leave this blank and the default room name will be used.",
            ["ui.play.room_capacity"] = "Supports 2–4 players • choose Individual or Team in the lobby",
            ["ui.play.room_code"] = "Room code",
            ["ui.play.available_rooms"] = "Available rooms",
            ["ui.play.manual_code"] = "Already have a room code?",
            ["ui.play.room_players"] = "Code {0}  •  Players {1}/{2}",
            ["ui.play.lobby_title"] = "Match Lobby",
            ["ui.play.lobby_subtitle"] = "Check the roster, choose your colour and match format, then begin",
            ["ui.play.match_setup"] = "Match setup",
            ["ui.play.start_match"] = "Start Match",
            ["ui.play.leave"] = "Leave Room",
            ["ui.play.waiting_host"] = "Waiting for the host to start…",
            ["ui.play.members"] = "Players in room",
            ["ui.play.waiting_player_slot"] = "Waiting for a player…",
            ["ui.play.you_badge"] = "You",
            ["ui.play.host_badge"] = "Host",
            ["ui.play.team_badge"] = "Team",
            ["ui.play.format"] = "Match format",
            ["ui.play.format_individual"] = "Individual",
            ["ui.play.format_team"] = "Team",
            ["ui.play.turn_time"] = "Turn time",
            ["ui.play.turn_rush"] = "Rush (60s)",
            ["ui.play.turn_short"] = "Short (90s)",
            ["ui.play.turn_normal"] = "Normal (120s)",
            ["ui.play.turn_long"] = "Long (180s)",
            ["ui.play.team_hint"] = "Pick Team 1 or Team 2 — need 2–4 players with at least one on each team",
            ["ui.play.pick_team"] = "Pick a team",
            ["ui.play.team_one"] = "Team 1",
            ["ui.play.team_two"] = "Team 2",
            ["ui.play.your_color"] = "Your colour",
            ["ui.color.red"] = "Red",
            ["ui.color.orange"] = "Orange",
            ["ui.color.yellow"] = "Yellow",
            ["ui.color.dark_green"] = "Dark green",
            ["ui.color.light_green"] = "Light green",
            ["ui.color.light_blue"] = "Light blue",
            ["ui.color.blue"] = "Blue",
            ["ui.color.purple"] = "Purple",
            ["ui.color.pink"] = "Pink",
            ["ui.color.brown"] = "Brown",
            ["ui.color.black"] = "Black",
            ["ui.color.grey"] = "Grey",
            ["ui.color.teal"] = "Teal",
            ["ui.play.err_create"] = "Could not create the room",
            ["ui.play.err_join"] = "Could not join the room",
            ["ui.play.err_already_session"] = "You are already in a room or connecting",
            ["ui.play.err_invalid_room"] = "This room entry is invalid or has expired",
            ["ui.play.err_room_full"] = "This room is full",
            ["ui.play.err_match_started"] = "This room has already started its match",
            ["ui.play.err_room_closed"] = "The room is closed or its host is unavailable",
            ["ui.play.err_network"] = "No usable LAN connection was found. Check Ethernet or Wi-Fi and try again",
            ["ui.play.err_transport"] = "Could not start the connection. Check the port and firewall, then try again",
            ["ui.play.err_not_joinable"] = "That room cannot be joined right now",
            ["ui.play.err_code_format"] = "That room code is not a valid format",
            ["ui.play.err_code_not_found"] = "No room with that code was found on this network",
            ["ui.play.connection_lost"] = "Connection lost — back to the room list",
            ["ui.play.need_host"] = "Only the host can start the match",
            ["ui.play.need_players"] = "Waiting for players to join…",
            ["ui.play.need_two_players"] = "Need at least 2 players",
            ["ui.play.need_both_teams"] = "Both teams need at least one member",

            // Match HUD
            ["ui.match.confirm"] = "Confirm Place",
            ["ui.match.clear"] = "Clear Draft",
            ["ui.match.pass"] = "Pass",
            ["ui.match.exchange"] = "Exchange Selected",
            ["ui.match.commands_open"] = "Commands  ▲",
            ["ui.match.commands_hide"] = "Hide Commands  ▼",
            ["ui.match.bag"] = "Bag",
            ["ui.match.score"] = "Score :",
            ["ui.match.tile_preview_points"] = "This tile scores {0} points on a normal square",
            ["ui.match.turn"] = "Turn",
            ["ui.match.time"] = "Time",
            ["ui.match.you"] = "You",
            ["ui.match.menu"] = "Menu",
            ["ui.match.preview_ok"] = "Draft OK — estimated score",
            ["ui.match.preview_bad"] = "Draft is not valid yet",
            ["ui.match.tile_points_hint"] = "Number under the symbol = official A-Math tile points",
            ["ui.match.premium_legend"] = "x2/x3 PIECE = tile multiplier   x2/x3 WORD = equation multiplier",
            ["ui.match.declare"] = "Declare flexible tile",
            ["ui.match.your_turn"] = "Your turn",
            ["ui.match.wait_turn"] = "Waiting for another player",
            ["ui.match.waiting_players"] = "Paused — waiting for disconnected players to return",
            ["ui.match.waiting_players_countdown"] = "Waiting for disconnected players… playing on in {0}s",
            ["ui.match.exchange_hint"] = "Tap the tiles to trade in, then press Exchange again",
            ["ui.match.idle_hint"] = "Pick a tile from your rack, then tap a board cell.\nEquations are validated and scored automatically.",
            ["ui.match.ask_ai"] = "Ask AI",

            // AI assistant
            ["ai.window.title"] = "AI Assistant",
            ["ai.window.mode_rules"] = "Rules",
            ["ai.window.mode_strategy"] = "Strategy",
            ["ai.window.send"] = "Ask",
            ["ai.window.close"] = "Close",
            ["ai.window.busy"] = "Thinking…",
            ["ai.window.placeholder"] = "Ask about the rules…",
            ["ai.error.mode_unavailable"] = "The AI assistant is not available right now.",
            ["ai.error.empty_question"] = "Type a question first.",
            ["ai.error.unsafe_response"] = "That answer was withheld because it failed a safety check.",
            ["ai.error.request_failed"] = "Could not reach the AI assistant. Please try again.",
            ["ai.error.mode_not_ready"] = "This mode cannot answer at this point in the match.",
            ["ai.strategy.header"] = "Tips from the current board (scripted):",
            ["ai.strategy.no_tips"] =
                "Nothing specific stands out right now — look for an equation where both sides of '=' " +
                "are equal and that connects to the board, then reassess.",
            ["ai.strategy.question_ignored"] =
                "Your question was noted, but this mode reads the board state rather than free text.",
            ["ai.strategy.rejection"] =
                "Your last placement was rejected: {0} Rebuild the equation so both sides of '=' match and it connects to the existing board.",
            ["ai.strategy.first_move_center"] =
                "The opening move must cover the centre square and be at least 3 tiles long, for example 1+2=3.",
            ["ai.strategy.missing_equals"] =
                "You have no '=' (or blank to stand in for one) — exchange tiles, or wait for an '=' before attempting a long equation.",
            ["ai.strategy.low_bag"] =
                "The bag is nearly empty — leftover tiles cost you points at the end, and whoever goes out first collects everyone else's.",
            ["ai.strategy.few_numbers"] =
                "You are short on numbers — exchange a few tiles, or look for a short equation if you hold a matching pair and an '='.",
            ["ai.strategy.equals_but_stuck"] =
                "You have enough for an a+b=c equation — extend from a tile already on the board along one row or column, with no gaps.",

            // Pause
            ["ui.pause.title"] = "Paused",
            ["ui.pause.resume"] = "Back To Game",
            ["ui.pause.play_on"] = "Play On Without Them",
            ["ui.pause.settings"] = "Settings",
            ["ui.pause.leave"] = "Leave Room",

            // Results
            ["ui.result.title"] = "Match Results",
            ["ui.result.rematch"] = "Rematch",
            ["ui.result.leave"] = "Back to Menu",
            ["ui.result.winner"] = "Winner",
            ["ui.result.outcome"] = "Result",
            ["ui.result.draw"] = "Draw",
            ["ui.result.standings"] = "Standings",
            ["ui.result.team_winner"] = "Winning team: Team {0}",
            ["ui.result.team_standings"] = "Team totals",
            ["ui.result.duration_min"] = "Time {0}m {1}s",
            ["ui.result.duration_sec"] = "Time {0}s",
            ["ui.history.draw"] = "Draw",
            ["ui.history.recovered"] = "History was recovered from its backup.",
            ["ui.history.rebuilt"] = "History was rebuilt from replay files.",
            ["ui.history.storage_unavailable"] = "Match-history storage is unavailable.",

            // Recovery
            ["ui.recovery.title"] = "Connection Lost",
            ["ui.recovery.wait"] = "Keep Waiting",
            ["ui.recovery.leave"] = "Leave Room",
            ["ui.recovery.end"] = "End From Backup Save",
            ["ui.recovery.grace"] = "Reconnecting…",
            ["ui.recovery.search"] = "Searching the LAN for the room…",
            ["ui.recovery.promote"] = "Reconnecting…",
            ["ui.recovery.reconnect"] = "Room found — reconnecting…",
            ["ui.recovery.recovered"] = "Reconnected!",
        };
    }
}
