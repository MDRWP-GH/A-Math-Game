using System;
using System.Collections.Generic;
using AMath.Core;

namespace AMath.Gameplay.Board
{
    /// <summary>One cell inside a formed line, annotated for scoring.</summary>
    public struct LineCell
    {
        public byte X;
        public byte Y;
        /// <summary>Physical tile id (used for points; blank scores 0).</summary>
        public byte TileId;
        /// <summary>Effective id (used for equation math).</summary>
        public byte EffectiveTileId;
        /// <summary>True when placed this turn (premiums only apply to new tiles).</summary>
        public bool IsNew;
    }

    /// <summary>A complete maximal line (equation) formed by a placement.</summary>
    public sealed class FormedLine
    {
        public readonly List<LineCell> Cells = new(GameRules.BoardSize);
    }

    /// <summary>Outcome of placement validation.</summary>
    public sealed class PlacementValidation
    {
        public bool IsValid;
        public string Error;
        /// <summary>All equations formed by this placement (input to scoring).</summary>
        public readonly List<FormedLine> Lines = new(4);
    }

    /// <summary>
    /// Full A-Math placement validation. This is the heart of "never trust
    /// clients": the host runs it against its authoritative board and rack
    /// state for every incoming request. Checks are ordered cheapest-first so
    /// malicious or malformed requests are rejected with minimal work.
    /// </summary>
    public sealed class PlacementValidator
    {
        #region Public API

        /// <summary>
        /// Validates a placement request against the current board and the
        /// acting player's rack. Also collects the formed lines for scoring.
        /// </summary>
        public PlacementValidation Validate(BoardGrid board, IReadOnlyList<byte> rack, IReadOnlyList<TilePlacement> placements)
        {
            var result = new PlacementValidation();

            if (!CheckBasics(board, placements, out result.Error)) return result;
            if (!CheckDeclarations(placements, out result.Error)) return result;
            if (!CheckRackOwnership(rack, placements, out result.Error)) return result;
            if (!CheckGeometry(board, placements, out bool horizontal, out result.Error)) return result;
            if (!CollectLines(board, placements, horizontal, result)) return result;

            // Every formed line must be a mathematically valid equation.
            var effectiveIds = new List<byte>(GameRules.BoardSize);
            foreach (FormedLine line in result.Lines)
            {
                effectiveIds.Clear();
                foreach (LineCell cell in line.Cells)
                    effectiveIds.Add(cell.EffectiveTileId);

                if (!EquationEvaluator.IsValidEquation(effectiveIds, out string equationError))
                {
                    result.Error = equationError;
                    return result;
                }
            }

            result.IsValid = true;
            return result;
        }

        #endregion

        #region Checks

        private static bool CheckBasics(BoardGrid board, IReadOnlyList<TilePlacement> placements, out string error)
        {
            error = null;

            if (placements == null || placements.Count == 0 || placements.Count > GameRules.RackSize)
            {
                error = "Invalid number of tiles.";
                return false;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                TilePlacement p = placements[i];
                if (!BoardGrid.InBounds(p.X, p.Y))
                {
                    error = "Tile placed outside the board.";
                    return false;
                }

                if (board.IsOccupied(p.X, p.Y))
                {
                    error = "Cell is already occupied.";
                    return false;
                }

                for (int j = i + 1; j < placements.Count; j++)
                {
                    if (placements[j].X == p.X && placements[j].Y == p.Y)
                    {
                        error = "Two tiles on the same cell.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool CheckDeclarations(IReadOnlyList<TilePlacement> placements, out string error)
        {
            error = null;
            foreach (TilePlacement p in placements)
            {
                if (AMathTileSet.RequiresDeclaration(p.TileId))
                {
                    if (p.DeclaredAs == TilePlacement.NoDeclaration || !AMathTileSet.IsLegalDeclaration(p.TileId, p.DeclaredAs))
                    {
                        error = $"Tile {AMathTileSet.SymbolOf(p.TileId)} needs a valid declaration.";
                        return false;
                    }
                }
                else if (p.DeclaredAs != TilePlacement.NoDeclaration)
                {
                    error = "Regular tiles cannot be redeclared.";
                    return false;
                }
            }

            return true;
        }

        private static bool CheckRackOwnership(IReadOnlyList<byte> rack, IReadOnlyList<TilePlacement> placements, out string error)
        {
            error = null;

            if (rack == null)
            {
                error = "You do not own those tiles.";
                return false;
            }

            // Multiset containment: the request may not use more copies of a
            // tile than the rack actually holds. This blocks tile spawning.
            Span<int> needed = stackalloc int[AMathTileSet.TileTypeCount];
            foreach (TilePlacement p in placements)
            {
                if (p.TileId >= AMathTileSet.TileTypeCount)
                {
                    error = "Unknown tile id.";
                    return false;
                }
                needed[p.TileId]++;
            }

            Span<int> owned = stackalloc int[AMathTileSet.TileTypeCount];
            for (int i = 0; i < rack.Count; i++)
            {
                // A rack is host state rather than request data, but a corrupt
                // snapshot or a bad restore must fail this command rather than
                // throw an index error out of the middle of it.
                if (!AMathTileSet.IsValidTileId(rack[i]))
                {
                    error = "Rack contains an unknown tile id.";
                    return false;
                }

                owned[rack[i]]++;
            }

            for (int id = 0; id < AMathTileSet.TileTypeCount; id++)
            {
                if (needed[id] > owned[id])
                {
                    error = "You do not own those tiles.";
                    return false;
                }
            }

            return true;
        }

        private static bool CheckGeometry(BoardGrid board, IReadOnlyList<TilePlacement> placements, out bool horizontal, out string error)
        {
            error = null;

            // Single axis.
            bool sameRow = true, sameCol = true;
            for (int i = 1; i < placements.Count; i++)
            {
                sameRow &= placements[i].Y == placements[0].Y;
                sameCol &= placements[i].X == placements[0].X;
            }

            if (!sameRow && !sameCol)
            {
                horizontal = false;
                error = "Tiles must be placed in a single row or column.";
                return false;
            }

            horizontal = sameRow;

            // Contiguity: every cell across the placement span must be filled
            // (either pre-existing or newly placed).
            GetSpan(placements, horizontal, out int fixedCoord, out int min, out int max);
            for (int c = min; c <= max; c++)
            {
                int x = horizontal ? c : fixedCoord;
                int y = horizontal ? fixedCoord : c;
                if (!board.IsOccupied(x, y) && !ContainsCell(placements, x, y))
                {
                    error = "Placed tiles must form one contiguous line.";
                    return false;
                }
            }

            if (board.IsEmpty)
            {
                // First move must cover the center square.
                if (!ContainsCell(placements, GameRules.CenterX, GameRules.CenterY))
                {
                    error = "The first equation must cover the center square.";
                    return false;
                }
            }
            else
            {
                // Must connect to the existing crossword.
                bool connects = false;
                foreach (TilePlacement p in placements)
                {
                    connects |= (p.X > 0 && board.IsOccupied(p.X - 1, p.Y))
                             || (p.X < GameRules.BoardSize - 1 && board.IsOccupied(p.X + 1, p.Y))
                             || (p.Y > 0 && board.IsOccupied(p.X, p.Y - 1))
                             || (p.Y < GameRules.BoardSize - 1 && board.IsOccupied(p.X, p.Y + 1));
                    if (connects) break;
                }

                if (!connects)
                {
                    error = "New tiles must connect to tiles already on the board.";
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Line collection

        private static bool CollectLines(BoardGrid board, IReadOnlyList<TilePlacement> placements, bool horizontal, PlacementValidation result)
        {
            // Main line along the placement axis.
            TilePlacement first = placements[0];
            FormedLine mainLine = BuildLine(board, placements, first.X, first.Y, horizontal);
            if (mainLine.Cells.Count >= 2)
                result.Lines.Add(mainLine);

            // Perpendicular lines through every new tile.
            foreach (TilePlacement p in placements)
            {
                FormedLine cross = BuildLine(board, placements, p.X, p.Y, !horizontal);
                if (cross.Cells.Count >= 2)
                    result.Lines.Add(cross);
            }

            if (result.Lines.Count == 0)
            {
                result.Error = "Placement does not form an equation.";
                return false;
            }

            return true;
        }

        /// <summary>Builds the maximal occupied run through (x, y) along one axis.</summary>
        private static FormedLine BuildLine(BoardGrid board, IReadOnlyList<TilePlacement> placements, int x, int y, bool horizontal)
        {
            int dx = horizontal ? 1 : 0;
            int dy = horizontal ? 0 : 1;

            // Walk back to the start of the run.
            int startX = x, startY = y;
            while (BoardGrid.InBounds(startX - dx, startY - dy)
                   && (board.IsOccupied(startX - dx, startY - dy) || ContainsCell(placements, startX - dx, startY - dy)))
            {
                startX -= dx;
                startY -= dy;
            }

            var line = new FormedLine();
            int cx = startX, cy = startY;
            while (BoardGrid.InBounds(cx, cy))
            {
                if (board.IsOccupied(cx, cy))
                {
                    PlacedTile cell = board.CellAt(cx, cy);
                    line.Cells.Add(new LineCell
                    {
                        X = (byte)cx,
                        Y = (byte)cy,
                        TileId = cell.TileId,
                        EffectiveTileId = cell.EffectiveTileId,
                        IsNew = false
                    });
                }
                else if (TryGetPlacement(placements, cx, cy, out TilePlacement placed))
                {
                    line.Cells.Add(new LineCell
                    {
                        X = (byte)cx,
                        Y = (byte)cy,
                        TileId = placed.TileId,
                        EffectiveTileId = placed.EffectiveTileId,
                        IsNew = true
                    });
                }
                else
                {
                    break;
                }

                cx += dx;
                cy += dy;
            }

            return line;
        }

        #endregion

        #region Helpers

        private static bool ContainsCell(IReadOnlyList<TilePlacement> placements, int x, int y) =>
            TryGetPlacement(placements, x, y, out _);

        private static bool TryGetPlacement(IReadOnlyList<TilePlacement> placements, int x, int y, out TilePlacement found)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].X == x && placements[i].Y == y)
                {
                    found = placements[i];
                    return true;
                }
            }

            found = default;
            return false;
        }

        private static void GetSpan(IReadOnlyList<TilePlacement> placements, bool horizontal, out int fixedCoord, out int min, out int max)
        {
            fixedCoord = horizontal ? placements[0].Y : placements[0].X;
            min = int.MaxValue;
            max = int.MinValue;
            foreach (TilePlacement p in placements)
            {
                int c = horizontal ? p.X : p.Y;
                if (c < min) min = c;
                if (c > max) max = c;
            }
        }

        #endregion
    }
}
