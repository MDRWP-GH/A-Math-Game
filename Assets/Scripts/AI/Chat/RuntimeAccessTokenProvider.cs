using AMath.AI.Interfaces;

namespace AMath.AI.Chat
{
    /// <summary>
    /// Holds an authenticated backend session token only in process memory.
    /// The authentication layer must set a newly issued short-lived token after
    /// sign-in; it is deliberately never written to a Unity asset or PlayerPrefs.
    /// </summary>
    public sealed class RuntimeAccessTokenProvider : IAccessTokenProvider
    {
        private string _accessToken;

        /// <inheritdoc />
        public string GetAccessToken() => _accessToken;

        /// <summary>Replaces the current token after a successful authentication exchange.</summary>
        public void SetAccessToken(string accessToken) => _accessToken = accessToken;

        /// <summary>Clears the token when signing out or leaving an authenticated session.</summary>
        public void Clear() => _accessToken = null;
    }
}
