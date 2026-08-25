using System.Collections.Generic;
using AMath.Gameplay.Board;

namespace AMath.Tutorial.Scripted
{
    /// <summary>Set-equality helpers for authored tutorial placements.</summary>
    public static class ScriptedPlacementRules
    {
        /// <summary>True when both placements name the same tile, cell and declaration.</summary>
        public static bool Matches(TilePlacement a, TilePlacement b) =>
            a.TileId == b.TileId && a.X == b.X && a.Y == b.Y && a.DeclaredAs == b.DeclaredAs;

        /// <summary>Finds the authored placement for a cell, if any.</summary>
        public static bool TryGetExpectedAt(
            IReadOnlyList<TilePlacement> expected,
            int x,
            int y,
            out TilePlacement required)
        {
            if (expected != null)
            {
                for (int i = 0; i < expected.Count; i++)
                {
                    if (expected[i].X == x && expected[i].Y == y)
                    {
                        required = expected[i];
                        return true;
                    }
                }
            }

            required = default;
            return false;
        }

        /// <summary>True when a pending draft occupies this cell.</summary>
        public static bool PendingOccupies(IReadOnlyList<TilePlacement> pending, int x, int y)
        {
            if (pending == null) return false;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].X == x && pending[i].Y == y)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True when every authored placement is present in the draft
        /// (order-independent) and the draft has no extras.
        /// </summary>
        public static bool IsComplete(
            IReadOnlyList<TilePlacement> expected,
            IReadOnlyList<TilePlacement> pending)
        {
            if (expected == null || pending == null || expected.Count != pending.Count)
                return false;

            var used = new bool[pending.Count];
            for (int i = 0; i < expected.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < pending.Count; j++)
                {
                    if (used[j]) continue;
                    if (!Matches(expected[i], pending[j])) continue;
                    used[j] = true;
                    found = true;
                    break;
                }

                if (!found) return false;
            }

            return true;
        }

        /// <summary>Authored placements that are not yet in the draft.</summary>
        public static List<TilePlacement> Remaining(
            IReadOnlyList<TilePlacement> expected,
            IReadOnlyList<TilePlacement> pending)
        {
            var remaining = new List<TilePlacement>();
            if (expected == null) return remaining;

            for (int i = 0; i < expected.Count; i++)
            {
                if (!PendingOccupies(pending, expected[i].X, expected[i].Y))
                    remaining.Add(expected[i]);
            }

            return remaining;
        }
    }
}
