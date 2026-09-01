using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Shared pieces of a full-screen modal: root, dim catcher, optional glass card, fade.
    /// </summary>
    internal sealed class OverlayShell
    {
        public GameObject Root { get; }
        public Image Catcher { get; }
        public Image Card { get; }
        public OverlayFade Fade { get; }

        public OverlayShell(GameObject root, Image catcher, Image card, OverlayFade fade)
        {
            Root = root;
            Catcher = catcher;
            Card = card;
            Fade = fade;
        }

        public void Open()
        {
            if (Fade != null)
                Fade.FadeIn();
            else
                Root.SetActive(true);
        }

        public void Close()
        {
            if (Fade != null)
                Fade.FadeOut();
            else
                Root.SetActive(false);
        }

        public bool IsOpen => Root != null && Root.activeSelf;
    }
}
