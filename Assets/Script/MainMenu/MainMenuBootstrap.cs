using UnityEngine;
using UnityEngine.SceneManagement;

namespace AMath.UI
{
    /// <summary>
    /// Creates the menu only for the first scene in Build Settings, so no scene wiring is needed.
    /// </summary>
    internal static class MainMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateMenuForStartupScene()
        {
            if (SceneManager.GetActiveScene().buildIndex != 0 ||
                Object.FindFirstObjectByType<MainMenuController>() != null)
            {
                return;
            }

            var menuRoot = new GameObject("Main Menu");
            menuRoot.AddComponent<MainMenuController>();
        }
    }
}
