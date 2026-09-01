using System;
using UnityEngine;

namespace AMath.Core
{
    /// <summary>
    /// The colours a player can identify themselves with, in one place so the
    /// lobby picker, the match HUD and host-side validation can never disagree
    /// on how many there are or what id maps to which colour.
    ///
    /// A colour is stored and replicated as its index (see
    /// <see cref="PlayerIdentity.ColorId"/>), never as RGB, so the palette can
    /// be retuned without invalidating saves, replays or in-flight matches.
    /// </summary>
    public static class PlayerColorPalette
    {
        /// <summary>Colour used when an id is missing or out of range.</summary>
        public const byte FallbackId = 0;

        private static readonly Color32[] Colors =
        {
            new(222, 58, 52, 255),    // 0  red
            new(240, 138, 32, 255),   // 1  orange
            new(245, 214, 62, 255),   // 2  yellow
            new(34, 118, 58, 255),    // 3  dark green
            new(128, 208, 96, 255),   // 4  light green
            new(98, 190, 240, 255),   // 5  light blue
            new(44, 90, 208, 255),    // 6  blue
            new(146, 78, 200, 255),   // 7  purple
            new(240, 130, 180, 255),  // 8  pink
            new(132, 86, 48, 255),    // 9  brown
            new(26, 26, 30, 255),     // 10 black
            new(140, 145, 155, 255),  // 11 grey
            new(32, 176, 168, 255)    // 12 teal
        };

        private static readonly string[] NameKeys =
        {
            "ui.color.red",
            "ui.color.orange",
            "ui.color.yellow",
            "ui.color.dark_green",
            "ui.color.light_green",
            "ui.color.light_blue",
            "ui.color.blue",
            "ui.color.purple",
            "ui.color.pink",
            "ui.color.brown",
            "ui.color.black",
            "ui.color.grey",
            "ui.color.teal"
        };

        /// <summary>Number of selectable colours.</summary>
        public static int Count => Colors.Length;

        /// <summary>True when <paramref name="colorId"/> names a real colour.</summary>
        public static bool IsValid(byte colorId) => colorId < Colors.Length;

        /// <summary>
        /// Colour for an id, falling back rather than throwing: an out-of-range
        /// id means stale or hostile data, which should not break rendering.
        /// </summary>
        public static Color32 ColorOf(byte colorId) =>
            Colors[IsValid(colorId) ? colorId : FallbackId];

        /// <summary>Localization key for a colour's display name.</summary>
        public static string NameKeyOf(byte colorId) =>
            NameKeys[IsValid(colorId) ? colorId : FallbackId];

        /// <summary>
        /// First colour at or after <paramref name="searchStart"/> that
        /// <paramref name="isTaken"/> rejects, wrapping around the palette.
        ///
        /// Callers pass a random <paramref name="searchStart"/> to get "a random
        /// free colour" while still guaranteeing the result is actually free —
        /// picking at random and retrying could collide forever. When every
        /// colour is taken the start itself is returned, so more players than
        /// colours degrades to a duplicate instead of failing to seat anyone.
        /// </summary>
        public static byte FirstUnused(Func<byte, bool> isTaken, int searchStart)
        {
            int count = Colors.Length;
            int start = ((searchStart % count) + count) % count;

            if (isTaken == null)
                return (byte)start;

            for (int offset = 0; offset < count; offset++)
            {
                var candidate = (byte)((start + offset) % count);
                if (!isTaken(candidate))
                    return candidate;
            }

            return (byte)start;
        }
    }
}
