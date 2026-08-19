using System.Text;

namespace AMath.Art
{
    /// <summary>
    /// Remaps Thai upper/lower vowels and tone marks to pre-raised private-use
    /// glyphs in the project K2D font so Unity's legacy UI Text does not draw
    /// them sunk into consonants (e.g. เริ่ม, ตั้งค่า, วิธีเล่น).
    /// </summary>
    public static class ThaiFontAdjuster
    {
        // Raised upper: ั ิ ี ึ ื ็ ํ  -> F701-F707
        // Raised tone on base: ่ ้ ๊ ๋ ์ -> F708-F70C
        // Stacked tone above upper:         -> F70D-F711
        // Upper.left on ascender base:     -> F712-F718
        // Tone.left on ascender base:      -> F719-F71D
        // Lower.low under descender: ุ ู ฺ -> F71E-F720
        // Descless ญ / ฐ:                 -> F721 / F722

        public static bool IsThaiString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c >= '\x0E00' && c <= '\x0E7F')
                {
                    return true;
                }
            }

            return false;
        }

        public static string Adjust(string s)
        {
            if (string.IsNullOrEmpty(s) || !IsThaiString(s))
            {
                return s;
            }

            var length = s.Length;
            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                var c = s[i];

                if (c == '\x0E0D' && i < length - 1 && IsLower(s[i + 1]))
                {
                    sb.Append('\xF721');
                    continue;
                }

                if (c == '\x0E10' && i < length - 1 && IsLower(s[i + 1]))
                {
                    sb.Append('\xF722');
                    continue;
                }

                if (IsLower(c) && i > 0 && IsBaseDesc(s[i - 1]))
                {
                    sb.Append((char)('\xF71E' + (c - '\x0E38')));
                    continue;
                }

                if (IsUpper(c))
                {
                    var baseChar = i > 0 ? s[i - 1] : '\0';
                    if (IsBaseAsc(baseChar))
                    {
                        sb.Append(ToUpperLeft(c));
                    }
                    else
                    {
                        sb.Append(ToRaisedUpper(c));
                    }

                    continue;
                }

                if (IsTop(c) && i > 0)
                {
                    var prev = s[i - 1];
                    if (IsUpper(prev) || IsRaisedUpper(prev) || IsUpperLeft(prev))
                    {
                        // Tone sits above an upper vowel.
                        sb.Append(ToStackedTone(c));
                        continue;
                    }

                    var baseChar = prev;
                    if (IsLower(prev) && i > 1)
                    {
                        baseChar = s[i - 2];
                    }

                    if (IsBaseAsc(baseChar))
                    {
                        sb.Append(ToToneLeft(c));
                    }
                    else
                    {
                        sb.Append(ToRaisedTone(c));
                    }

                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static char ToRaisedUpper(char c) => c switch
        {
            '\x0E31' => '\xF701',
            '\x0E34' => '\xF702',
            '\x0E35' => '\xF703',
            '\x0E36' => '\xF704',
            '\x0E37' => '\xF705',
            '\x0E47' => '\xF706',
            '\x0E4D' => '\xF707',
            _ => c
        };

        private static char ToUpperLeft(char c) => c switch
        {
            '\x0E31' => '\xF712',
            '\x0E34' => '\xF713',
            '\x0E35' => '\xF714',
            '\x0E36' => '\xF715',
            '\x0E37' => '\xF716',
            '\x0E47' => '\xF717',
            '\x0E4D' => '\xF718',
            _ => c
        };

        private static char ToRaisedTone(char c) => (char)('\xF708' + (c - '\x0E48'));

        private static char ToStackedTone(char c) => (char)('\xF70D' + (c - '\x0E48'));

        private static char ToToneLeft(char c) => (char)('\xF719' + (c - '\x0E48'));

        private static bool IsBaseDesc(char c) => c == '\x0E0E' || c == '\x0E0F';

        private static bool IsBaseAsc(char c) =>
            c == '\x0E1B' || c == '\x0E1D' || c == '\x0E1F' || c == '\x0E2C';

        private static bool IsTop(char c) => c >= '\x0E48' && c <= '\x0E4C';

        private static bool IsLower(char c) => c >= '\x0E38' && c <= '\x0E3A';

        private static bool IsUpper(char c) =>
            c == '\x0E31' || c == '\x0E34' || c == '\x0E35' || c == '\x0E36' ||
            c == '\x0E37' || c == '\x0E47' || c == '\x0E4D';

        private static bool IsRaisedUpper(char c) => c >= '\xF701' && c <= '\xF707';

        private static bool IsUpperLeft(char c) => c >= '\xF712' && c <= '\xF718';
    }
}
