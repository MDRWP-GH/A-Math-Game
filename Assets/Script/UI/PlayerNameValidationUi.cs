using AMath.Core.Identity;

namespace AMath.UI
{
    internal static class PlayerNameValidationUi
    {
        public static string LocalizationKey(PlayerNameValidationError error) => error switch
        {
            PlayerNameValidationError.Required => "ui.player_name.err_required",
            PlayerNameValidationError.TooLong => "ui.player_name.err_too_long",
            PlayerNameValidationError.UnsupportedCharacter => "ui.player_name.err_unsupported",
            _ => string.Empty
        };
    }
}
