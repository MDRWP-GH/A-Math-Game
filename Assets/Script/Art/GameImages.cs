using UnityEngine;

namespace AMath.Art
{
    /// <summary>
    /// Loads sprites from Assets/Resources/Images at runtime.
    /// Drop PNG files into the matching subfolder (UI, Icons, Backgrounds),
    /// set Texture Type to Sprite (2D and UI), then load by name without extension.
    /// </summary>
    public static class GameImages
    {
        private const string Root = "Images";

        public static Sprite LoadUi(string name) => Load(Root + "/UI/" + name);

        public static Sprite LoadIcon(string name) => Load(Root + "/Icons/" + name);

        public static Sprite LoadBackground(string name) => Load(Root + "/Backgrounds/" + name);

        public static Sprite Load(string resourcePath) => Resources.Load<Sprite>(resourcePath);
    }
}
