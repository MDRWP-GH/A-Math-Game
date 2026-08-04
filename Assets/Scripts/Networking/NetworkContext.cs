using AMath.Core;

namespace AMath.Networking
{
    /// <summary>
    /// Bridge that lets Mirror-spawned objects (player prefabs, scene network
    /// behaviours) reach the composition root's services.
    ///
    /// Mirror instantiates networked prefabs itself, so constructor injection
    /// is impossible for them. This single, explicitly-scoped locator — set by
    /// the composition root, cleared when it is destroyed — is the one
    /// controlled exception to "no static access", confined to the networking
    /// assembly's spawn boundary.
    /// </summary>
    public static class NetworkContext
    {
        /// <summary>Active service registry; null outside a running session.</summary>
        public static ServiceRegistry Services { get; private set; }

        /// <summary>Installed by the composition root on startup.</summary>
        public static void Install(ServiceRegistry services) => Services = services;

        /// <summary>Cleared by the composition root on teardown.</summary>
        public static void Clear() => Services = null;
    }
}
