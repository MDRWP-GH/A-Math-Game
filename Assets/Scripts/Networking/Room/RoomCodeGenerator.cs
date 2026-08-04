using System.Text;

namespace AMath.Networking.Room
{
    /// <summary>
    /// Generates 6-character room codes such as "AB29KF".
    ///
    /// The alphabet excludes visually ambiguous characters (0/O, 1/I/L) so a
    /// code can be read aloud across the room without mistakes. Codes are pure
    /// user convenience: they are always resolved to a discovered host IP
    /// before connecting and are never a trust or routing mechanism.
    /// </summary>
    public static class RoomCodeGenerator
    {
        /// <summary>Characters allowed in room codes.</summary>
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        /// <summary>Length of every room code.</summary>
        public const int CodeLength = 6;

        /// <summary>
        /// Creates a new random code. Uses System.Random deliberately: room
        /// codes are lobby-scoped and must NOT consume the deterministic
        /// gameplay stream.
        /// </summary>
        public static string Generate()
        {
            var random = new System.Random();
            var builder = new StringBuilder(CodeLength);
            for (int i = 0; i < CodeLength; i++)
                builder.Append(Alphabet[random.Next(Alphabet.Length)]);
            return builder.ToString();
        }

        /// <summary>Validates user input before attempting resolution.</summary>
        public static bool IsValidFormat(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != CodeLength)
                return false;

            foreach (char c in code.ToUpperInvariant())
            {
                if (Alphabet.IndexOf(c) < 0)
                    return false;
            }

            return true;
        }
    }
}
