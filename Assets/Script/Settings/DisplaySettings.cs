using System;
using System.Collections.Generic;
using UnityEngine;

namespace AMath.Settings
{
    /// <summary>
    /// How the game window is presented. Kept separate from <see cref="FullScreenMode"/> so the
    /// three choices the player sees stay stable even where Unity maps them differently per platform.
    /// </summary>
    public enum ViewMode
    {
        Window = 0,
        FullScreen = 1,
        Borderless = 2
    }

    /// <summary>
    /// A selectable screen size. Unity's <see cref="Resolution"/> also carries a refresh rate, which
    /// would make the list repeat the same size several times.
    /// </summary>
    public readonly struct ScreenSize : IEquatable<ScreenSize>
    {
        public ScreenSize(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
        }

        public int Width { get; }

        public int Height { get; }

        public float Aspect => Width / (float)Height;

        public string Label => $"{Width}x{Height} {AspectLabel}";

        public string AspectLabel
        {
            get
            {
                foreach (var candidate in CommonAspects)
                {
                    if (Mathf.Abs(Aspect - candidate.Ratio) < 0.02f)
                    {
                        return candidate.Label;
                    }
                }

                var divisor = GreatestCommonDivisor(Width, Height);
                var x = Width / divisor;
                var y = Height / divisor;

                // Sizes such as 1366x768 reduce to 683:384, which reads worse than a decimal ratio.
                return y <= 32 ? $"{x}:{y}" : $"{Aspect:0.00}:1";
            }
        }

        public bool Equals(ScreenSize other) => Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is ScreenSize other && Equals(other);

        public override int GetHashCode() => (Width * 397) ^ Height;

        public override string ToString() => Label;

        private static readonly (float Ratio, string Label)[] CommonAspects =
        {
            (4f / 3f, "4:3"),
            (5f / 4f, "5:4"),
            (3f / 2f, "3:2"),
            (16f / 10f, "16:10"),
            (16f / 9f, "16:9"),
            (21f / 9f, "21:9"),
            (32f / 9f, "32:9")
        };

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                var remainder = a % b;
                a = b;
                b = remainder;
            }

            return Mathf.Max(1, a);
        }
    }

    /// <summary>
    /// Owns the window presentation settings: which view mode is active, which screen size is
    /// selected, and how both are stored between sessions.
    /// </summary>
    public static class DisplaySettings
    {
        private const string ViewModeKey = "amath.display.viewMode";
        private const string WidthKey = "amath.display.width";
        private const string HeightKey = "amath.display.height";

        private const int MinimumWidth = 1024;
        private const int MinimumHeight = 576;

        private static readonly ScreenSize[] FallbackSizes =
        {
            new ScreenSize(1280, 720),
            new ScreenSize(1366, 768),
            new ScreenSize(1600, 900),
            new ScreenSize(1920, 1080),
            new ScreenSize(2560, 1440),
            new ScreenSize(3840, 2160)
        };

        private static readonly List<ScreenSize> Sizes = new List<ScreenSize>();

        /// <summary>Raised after the view mode or the screen size changed.</summary>
        public static event Action Changed;

        public static readonly string[] ViewModeLabels = { "Window", "Full Screen", "Borderless" };

        public static ViewMode ViewMode { get; private set; }

        public static ScreenSize Resolution { get; private set; }

        public static IReadOnlyList<ScreenSize> AvailableSizes => Sizes;

        public static int ResolutionIndex
        {
            get
            {
                var index = Sizes.IndexOf(Resolution);
                return index < 0 ? 0 : index;
            }
        }

        static DisplaySettings()
        {
            BuildSizeList();
            Load();
        }

        /// <summary>
        /// Applies the stored settings before the first scene runs so the player keeps the window
        /// they chose last session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStoredSettings()
        {
            Apply(ViewMode, Resolution, save: false);
        }

        public static void Apply(ViewMode viewMode, ScreenSize size, bool save = true)
        {
            ViewMode = IsDefined(viewMode) ? viewMode : ViewMode.Window;
            Resolution = ClosestAvailable(size);

            ApplyToScreen();

            if (save)
            {
                Save();
            }

            Changed?.Invoke();
        }

        public static void SetViewMode(ViewMode viewMode) => Apply(viewMode, Resolution);

        public static void SetResolution(ScreenSize size) => Apply(ViewMode, size);

        public static FullScreenMode ToFullScreenMode(ViewMode viewMode)
        {
            switch (viewMode)
            {
                case ViewMode.FullScreen:
                    return FullScreenMode.ExclusiveFullScreen;
                case ViewMode.Borderless:
                    return FullScreenMode.FullScreenWindow;
                default:
                    return FullScreenMode.Windowed;
            }
        }

        private static void ApplyToScreen()
        {
#if UNITY_EDITOR
            // Screen.SetResolution is ignored by the editor, so report the choice instead of
            // pretending the game view changed.
            Debug.Log($"A-Math: display set to {Resolution.Label} ({ViewModeLabels[(int)ViewMode]}). Play a built player to see it take effect.");
#else
            Screen.SetResolution(Resolution.Width, Resolution.Height, ToFullScreenMode(ViewMode));
#endif
        }

        private static void Load()
        {
            var storedMode = (ViewMode)PlayerPrefs.GetInt(ViewModeKey, (int)FromFullScreenMode(Screen.fullScreenMode));
            ViewMode = IsDefined(storedMode) ? storedMode : ViewMode.Window;

            var width = PlayerPrefs.GetInt(WidthKey, Screen.width);
            var height = PlayerPrefs.GetInt(HeightKey, Screen.height);
            Resolution = ClosestAvailable(new ScreenSize(width, height));
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(ViewModeKey, (int)ViewMode);
            PlayerPrefs.SetInt(WidthKey, Resolution.Width);
            PlayerPrefs.SetInt(HeightKey, Resolution.Height);
            PlayerPrefs.Save();
        }

        private static ViewMode FromFullScreenMode(FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.ExclusiveFullScreen:
                    return ViewMode.FullScreen;
                case FullScreenMode.FullScreenWindow:
                case FullScreenMode.MaximizedWindow:
                    return ViewMode.Borderless;
                default:
                    return ViewMode.Window;
            }
        }

        private static bool IsDefined(ViewMode viewMode)
        {
            return viewMode >= ViewMode.Window && viewMode <= ViewMode.Borderless;
        }

        /// <summary>
        /// Picks the closest entry in the list so a stored size that the current monitor no longer
        /// supports still resolves to something selectable.
        /// </summary>
        private static ScreenSize ClosestAvailable(ScreenSize requested)
        {
            if (Sizes.Count == 0)
            {
                return requested;
            }

            var bestIndex = 0;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < Sizes.Count; i++)
            {
                var candidate = Sizes[i];
                if (candidate.Equals(requested))
                {
                    return candidate;
                }

                var distance = Mathf.Abs(candidate.Width - requested.Width) + Mathf.Abs(candidate.Height - requested.Height);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return Sizes[bestIndex];
        }

        private static void BuildSizeList()
        {
            var maxWidth = Mathf.Max(Screen.currentResolution.width, Screen.width);
            var maxHeight = Mathf.Max(Screen.currentResolution.height, Screen.height);

            foreach (var resolution in Screen.resolutions)
            {
                // Whatever the platform reports is by definition supported, so it only has to pass
                // the readability floor.
                AddSize(new ScreenSize(resolution.width, resolution.height));
                maxWidth = Mathf.Max(maxWidth, resolution.width);
                maxHeight = Mathf.Max(maxHeight, resolution.height);
            }

            // A headless build or an editor with a restricted display list can report almost
            // nothing, so top the list up with sizes that still fit on this monitor.
            if (Sizes.Count < 3)
            {
                foreach (var fallback in FallbackSizes)
                {
                    if (fallback.Width <= maxWidth && fallback.Height <= maxHeight)
                    {
                        AddSize(fallback);
                    }
                }
            }

            // The size the game is running at right now is always a valid choice.
            var current = new ScreenSize(Screen.width, Screen.height);
            if (!Sizes.Contains(current))
            {
                Sizes.Add(current);
            }

            Sizes.Sort(CompareBySize);
        }

        private static void AddSize(ScreenSize size)
        {
            if (size.Width < MinimumWidth || size.Height < MinimumHeight)
            {
                return;
            }

            if (!Sizes.Contains(size))
            {
                Sizes.Add(size);
            }
        }

        private static int CompareBySize(ScreenSize a, ScreenSize b)
        {
            return a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height);
        }
    }
}
