namespace AMath.AI.Interfaces
{
    /// <summary>
    /// Supplies the short-lived bearer/session token used to authenticate the
    /// Unity client to the application's AI backend proxy. Implementations
    /// obtain the token from the runtime identity/session layer; tokens are
    /// never serialized into Unity assets or source control.
    /// </summary>
    public interface IAccessTokenProvider
    {
        /// <summary>Returns the current bearer token, or null when the player is not authenticated.</summary>
        string GetAccessToken();
    }
}
