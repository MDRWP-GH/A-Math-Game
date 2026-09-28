namespace AMath.Networking.Room
{
    /// <summary>Stable, UI-independent reason why creating or joining a room failed.</summary>
    public enum RoomOperationError
    {
        None = 0,
        AlreadyInSession = 1,
        InvalidRoom = 2,
        RoomFull = 3,
        MatchStarted = 4,
        CodeInvalid = 5,
        CodeNotFound = 6,
        RoomClosed = 7,
        NetworkUnavailable = 8,
        TransportFailed = 9
    }

    /// <summary>Maps room-domain failures to keys without embedding a language in networking code.</summary>
    public static class RoomOperationErrorText
    {
        public static string LocalizationKey(RoomOperationError error) => error switch
        {
            RoomOperationError.AlreadyInSession => "ui.play.err_already_session",
            RoomOperationError.InvalidRoom => "ui.play.err_invalid_room",
            RoomOperationError.RoomFull => "ui.play.err_room_full",
            RoomOperationError.MatchStarted => "ui.play.err_match_started",
            RoomOperationError.CodeInvalid => "ui.play.err_code_format",
            RoomOperationError.CodeNotFound => "ui.play.err_code_not_found",
            RoomOperationError.RoomClosed => "ui.play.err_room_closed",
            RoomOperationError.NetworkUnavailable => "ui.play.err_network",
            RoomOperationError.TransportFailed => "ui.play.err_transport",
            _ => "ui.play.err_join"
        };
    }
}
