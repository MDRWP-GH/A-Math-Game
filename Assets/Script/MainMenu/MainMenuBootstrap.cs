using UnityEngine;
using UnityEngine.SceneManagement;

namespace AMath.UI
{
    /// <summary>
    /// Creates the menu only for the first scene in Build Settings, so no scene wiring is needed.
    /// </summary>
    internal static class MainMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            // Runtime initialization can run more than once when entering Play Mode without a
            // domain reload. Remove first so one scene load can never create duplicate menus.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureMenuForScene(scene);
        }

        internal static MainMenuController EnsureMenuForScene(Scene scene)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                scene.buildIndex != 0)
            {
                return null;
            }

            MainMenuController existing =
                Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            var menuRoot = new GameObject("Main Menu");
            SceneManager.MoveGameObjectToScene(menuRoot, scene);
            return menuRoot.AddComponent<MainMenuController>();
        }
    }
}
