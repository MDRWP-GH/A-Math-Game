using System.Globalization;
using System.Text;

namespace AMath.Core.Identity
{
    public enum PlayerNameValidationError
    {
        None,
        Required,
        TooLong,
        UnsupportedCharacter
    }

    /// <summary>One shared rule for names entered by the UI and accepted by a host.</summary>
    public static class PlayerNameValidator
    {
        public const int MaxLength = 24;

        public static bool TryNormalize(
            string value,
            out string normalized,
            out PlayerNameValidationError error)
        {
            normalized = string.Empty;
            error = PlayerNameValidationError.None;

            if (string.IsNullOrEmpty(value))
            {
                error = PlayerNameValidationError.Required;
                return false;
            }

            // Only an ordinary space belongs to the allowed character set.
            // Check before Trim so a newline or tab at either edge cannot be
            // quietly removed and accepted as a different name.
            foreach (char character in value)
            {
                if (character != ' ' && (char.IsWhiteSpace(character) || char.IsControl(character)))
                {
                    error = PlayerNameValidationError.UnsupportedCharacter;
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                error = PlayerNameValidationError.Required;
                return false;
            }

            try
            {
                normalized = value.Trim().Normalize(NormalizationForm.FormC);
            }
            catch (System.ArgumentException)
            {
                error = PlayerNameValidationError.UnsupportedCharacter;
                normalized = string.Empty;
                return false;
            }

            if (normalized.Length > MaxLength)
            {
                error = PlayerNameValidationError.TooLong;
                normalized = string.Empty;
                return false;
            }

            foreach (char character in normalized)
            {
                if (!IsAllowed(character))
                {
                    error = PlayerNameValidationError.UnsupportedCharacter;
                    normalized = string.Empty;
                    return false;
                }
            }

            return true;
        }

        private static bool IsAllowed(char character)
        {
            if (character == ' ' || character == '-' || character == '_')
                return true;

            if ((character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9'))
                return true;

            if (character < '\u0E00' || character > '\u0E7F')
                return false;

            UnicodeCategory category = char.GetUnicodeCategory(character);
            return category == UnicodeCategory.OtherLetter ||
                   category == UnicodeCategory.NonSpacingMark ||
                   category == UnicodeCategory.SpacingCombiningMark ||
                   category == UnicodeCategory.DecimalDigitNumber;
        }
    }
}
