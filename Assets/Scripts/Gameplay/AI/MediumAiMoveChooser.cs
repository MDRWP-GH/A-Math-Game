using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.RandomNumbers;
using AMath.Gameplay.Board;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Gameplay.AI
{
    /// <summary>
    /// Medium-strength scripted mover: enumerates simple equations that the
    /// rack can build — optionally reusing a tile already on the board —
    /// validates them on the real board, then picks from the mid band of
    /// scored options so the AI can win or lose.
    /// </summary>
    public sealed class MediumAiMoveChooser : IAiMoveChooser
    {
        /// <summary>
        /// How many symbols of an equation may come from tiles already on the
        /// board. Hooking onto one existing tile covers the ordinary A-Math
        /// continuation; a wider budget multiplies the search for very little
        /// extra playing strength.
        /// </summary>
        private const int MaxBoardSuppliedSymbols = 1;

        private readonly ScoreCalculator _scoreCalculator = new();
        private readonly List<ScoredMove> _candidates = new(64);
        private readonly List<List<byte>> _equations = new(64);
        private readonly List<TilePlacement> _placementBuffer = new(GameRules.RackSize);
        private readonly List<byte> _rackScratch = new(GameRules.RackSize);
        private readonly List<byte> _supplyScratch = new(GameRules.RackSize);
        private readonly List<(int x, int y)> _anchors = new(64);
        private readonly HashSet<string> _equationKeys = new();

        /// <summary>Effective symbols the board can currently stand in for.</summary>
        private readonly bool[] _boardSymbols = new bool[TileTypeCount];

        /// <inheritdoc />
        public IGameCommand Choose(
            BoardManager board,
            IReadOnlyList<byte> rack,
            int bagCount,
            int choiceSeed)
        {
            _candidates.Clear();
            BuildEquations(rack, board.Grid);
            CollectPlaceCandidates(board, rack);

            if (_candidates.Count > 0)
            {
                ScoredMove pick = PickMedium(_candidates, choiceSeed);
                var place = new PlaceTilesCommand();
                place.Placements.AddRange(pick.Placements);
                return place;
            }

            if (bagCount > 0 && rack.Count > 0)
            {
                var exchange = new ExchangeTilesCommand();
                int count = System.Math.Min(rack.Count, System.Math.Min(bagCount, 1 + (choiceSeed & 1)));
                for (int i = 0; i < count; i++)
                    exchange.TileIds.Add(rack[i]);
                return exchange;
            }

            return new PassTurnCommand();
        }

        private void BuildEquations(IReadOnlyList<byte> rack, BoardGrid grid)
        {
            _equations.Clear();
            _equationKeys.Clear();
            CollectBoardSymbols(grid);

            var numberValues = new List<byte>(24);
            for (byte n = 0; n <= 20; n++)
            {
                // Worth trying when this turn could source the number either
                // from the rack or from a tile already on the board.
                if (CanSupply(rack, n) || _boardSymbols[n])
                    numberValues.Add(n);
            }

            // a = a
            for (int i = 0; i < numberValues.Count; i++)
            {
                byte n = numberValues[i];
                if (CanSupplyEquation(rack, n, EqualsSign, n))
                    AddEquation(n, EqualsSign, n);
            }

            // a ⊕ b = c
            for (int i = 0; i < numberValues.Count; i++)
            {
                byte a = numberValues[i];
                for (int j = 0; j < numberValues.Count; j++)
                {
                    byte b = numberValues[j];
                    TryAddBinary(rack, a, b, Plus, a + b);
                    TryAddBinary(rack, a, b, Minus, a - b);
                    TryAddBinary(rack, a, b, Times, a * b);
                    if (b != 0 && a % b == 0)
                        TryAddBinary(rack, a, b, Divide, a / b);
                }
            }
        }

        private void TryAddBinary(IReadOnlyList<byte> rack, byte a, byte b, byte op, int result)
        {
            if (result < 0 || result > 20) return;
            byte c = (byte)result;
            if (!CanSupplyEquation(rack, a, op, b, EqualsSign, c)) return;
            AddEquation(a, op, b, EqualsSign, c);
        }

        /// <summary>
        /// Records which effective symbols sit on the board, so equation
        /// generation knows what a hook could borrow instead of demanding the
        /// rack cover every symbol itself.
        /// </summary>
        private void CollectBoardSymbols(BoardGrid grid)
        {
            System.Array.Clear(_boardSymbols, 0, _boardSymbols.Length);
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    if (!grid.IsOccupied(x, y)) continue;
                    byte effective = grid.CellAt(x, y).EffectiveTileId;
                    if (effective < _boardSymbols.Length)
                        _boardSymbols[effective] = true;
                }
            }
        }

        private void AddEquation(params byte[] ids)
        {
            string key = Key(ids);
            if (!_equationKeys.Add(key)) return;

            var eq = new List<byte>(ids.Length);
            eq.AddRange(ids);
            _equations.Add(eq);
        }

        private void CollectPlaceCandidates(BoardManager board, IReadOnlyList<byte> rack)
        {
            for (int e = 0; e < _equations.Count; e++)
            {
                List<byte> equation = _equations[e];
                if (board.Grid.IsEmpty)
                    TryPlaceCoveringCenter(board, rack, equation);
                else
                    TryPlaceAttached(board, rack, equation);

                if (_candidates.Count >= 96)
                    return;
            }
        }

        private void TryPlaceCoveringCenter(BoardManager board, IReadOnlyList<byte> rack, List<byte> equation)
        {
            int length = equation.Count;
            for (int start = GameRules.CenterX - (length - 1); start <= GameRules.CenterX; start++)
            {
                if (start < 0 || start + length > GameRules.BoardSize) continue;
                TryValidatePlacement(board, rack, equation, start, GameRules.CenterY, horizontal: true);
            }

            for (int start = GameRules.CenterY - (length - 1); start <= GameRules.CenterY; start++)
            {
                if (start < 0 || start + length > GameRules.BoardSize) continue;
                TryValidatePlacement(board, rack, equation, GameRules.CenterX, start, horizontal: false);
            }
        }

        private void TryPlaceAttached(BoardManager board, IReadOnlyList<byte> rack, List<byte> equation)
        {
            CollectAnchors(board.Grid);
            int length = equation.Count;

            for (int a = 0; a < _anchors.Count; a++)
            {
                (int ax, int ay) = _anchors[a];
                for (int offset = 0; offset < length; offset++)
                {
                    int startX = ax - offset;
                    if (startX >= 0 && startX + length <= GameRules.BoardSize)
                        TryValidatePlacement(board, rack, equation, startX, ay, horizontal: true);

                    int startY = ay - offset;
                    if (startY >= 0 && startY + length <= GameRules.BoardSize)
                        TryValidatePlacement(board, rack, equation, ax, startY, horizontal: false);
                }
            }
        }

        private void CollectAnchors(BoardGrid grid)
        {
            _anchors.Clear();
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    if (grid.IsOccupied(x, y)) continue;
                    bool nextToTile =
                        (x > 0 && grid.IsOccupied(x - 1, y))
                        || (x < GameRules.BoardSize - 1 && grid.IsOccupied(x + 1, y))
                        || (y > 0 && grid.IsOccupied(x, y - 1))
                        || (y < GameRules.BoardSize - 1 && grid.IsOccupied(x, y + 1));
                    if (nextToTile)
                        _anchors.Add((x, y));
                }
            }
        }

        private void TryValidatePlacement(
            BoardManager board,
            IReadOnlyList<byte> rack,
            List<byte> equation,
            int startX,
            int startY,
            bool horizontal)
        {
            if (!TryMapEquationToPlacements(board.Grid, rack, equation, startX, startY, horizontal, _placementBuffer))
                return;

            PlacementValidation validation = board.Validate(rack, _placementBuffer);
            if (!validation.IsValid) return;

            int score = _scoreCalculator.Calculate(validation.Lines, _placementBuffer.Count);
            var copy = new List<TilePlacement>(_placementBuffer.Count);
            copy.AddRange(_placementBuffer);
            _candidates.Add(new ScoredMove { Score = score, Placements = copy });
        }

        private bool TryMapEquationToPlacements(
            BoardGrid grid,
            IReadOnlyList<byte> rack,
            List<byte> equation,
            int startX,
            int startY,
            bool horizontal,
            List<TilePlacement> into)
        {
            into.Clear();
            if (!SpanMatchesBoard(grid, equation, startX, startY, horizontal))
                return false;

            _rackScratch.Clear();
            _rackScratch.AddRange(rack);

            for (int i = 0; i < equation.Count; i++)
            {
                int x = horizontal ? startX + i : startX;
                int y = horizontal ? startY : startY + i;

                // A tile already on the board covers its own symbol, so only the
                // gaps come out of the rack. This is what lets the AI continue an
                // existing equation instead of only playing on empty space.
                if (grid.IsOccupied(x, y))
                    continue;

                if (!TryTakeTile(_rackScratch, equation[i], out byte tileId, out byte declaredAs))
                    return false;

                into.Add(new TilePlacement
                {
                    TileId = tileId,
                    X = (byte)x,
                    Y = (byte)y,
                    DeclaredAs = declaredAs
                });
            }

            return into.Count > 0;
        }

        /// <summary>
        /// Board-only screen for a span, run before any rack bookkeeping: it has
        /// to fit, leave at least one cell for this turn to fill, and match every
        /// tile already sitting inside it. The cells just outside must be empty
        /// too, otherwise the line the host reads back would be longer than the
        /// equation being placed.
        /// </summary>
        private static bool SpanMatchesBoard(
            BoardGrid grid,
            List<byte> equation,
            int startX,
            int startY,
            bool horizontal)
        {
            int length = equation.Count;
            int endX = horizontal ? startX + length - 1 : startX;
            int endY = horizontal ? startY : startY + length - 1;

            if (!BoardGrid.InBounds(startX, startY) || !BoardGrid.InBounds(endX, endY))
                return false;

            int beforeX = horizontal ? startX - 1 : startX;
            int beforeY = horizontal ? startY : startY - 1;
            if (BoardGrid.InBounds(beforeX, beforeY) && grid.IsOccupied(beforeX, beforeY))
                return false;

            int afterX = horizontal ? endX + 1 : endX;
            int afterY = horizontal ? endY : endY + 1;
            if (BoardGrid.InBounds(afterX, afterY) && grid.IsOccupied(afterX, afterY))
                return false;

            bool hasEmptyCell = false;
            for (int i = 0; i < length; i++)
            {
                int x = horizontal ? startX + i : startX;
                int y = horizontal ? startY : startY + i;

                if (!grid.IsOccupied(x, y))
                {
                    hasEmptyCell = true;
                    continue;
                }

                if (grid.CellAt(x, y).EffectiveTileId != equation[i])
                    return false;
            }

            return hasEmptyCell;
        }

        private static bool TryTakeTile(List<byte> rack, byte neededEffective, out byte tileId, out byte declaredAs)
        {
            tileId = 0;
            declaredAs = TilePlacement.NoDeclaration;

            int exact = rack.IndexOf(neededEffective);
            if (exact >= 0)
            {
                tileId = neededEffective;
                rack.RemoveAt(exact);
                return true;
            }

            if (neededEffective == Plus || neededEffective == Minus)
            {
                int flex = rack.IndexOf(PlusOrMinus);
                if (flex >= 0)
                {
                    tileId = PlusOrMinus;
                    declaredAs = neededEffective;
                    rack.RemoveAt(flex);
                    return true;
                }
            }

            if (neededEffective == Times || neededEffective == Divide)
            {
                int flex = rack.IndexOf(TimesOrDivide);
                if (flex >= 0)
                {
                    tileId = TimesOrDivide;
                    declaredAs = neededEffective;
                    rack.RemoveAt(flex);
                    return true;
                }
            }

            int blank = rack.IndexOf(Blank);
            if (blank >= 0 && IsLegalDeclaration(Blank, neededEffective))
            {
                tileId = Blank;
                declaredAs = neededEffective;
                rack.RemoveAt(blank);
                return true;
            }

            return false;
        }

        private static bool CanSupply(IReadOnlyList<byte> rack, byte needed) =>
            CountSupply(rack, needed) > 0;

        /// <summary>
        /// True when the rack covers the whole equation, save for up to
        /// <see cref="MaxBoardSuppliedSymbols"/> symbols a board tile could
        /// stand in for. Whether the board really offers them in the right
        /// cells is settled later, when the equation is mapped onto a span.
        /// </summary>
        private bool CanSupplyEquation(IReadOnlyList<byte> rack, params byte[] ids)
        {
            _supplyScratch.Clear();
            _supplyScratch.AddRange(rack);

            int borrowed = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (TryTakeTile(_supplyScratch, ids[i], out _, out _))
                    continue;

                if (!_boardSymbols[ids[i]] || ++borrowed > MaxBoardSuppliedSymbols)
                    return false;
            }

            return true;
        }

        private static int CountSupply(IReadOnlyList<byte> rack, byte needed)
        {
            int n = 0;
            for (int i = 0; i < rack.Count; i++)
            {
                byte id = rack[i];
                if (id == needed
                    || (id == Blank && IsLegalDeclaration(Blank, needed))
                    || (id == PlusOrMinus && (needed == Plus || needed == Minus))
                    || (id == TimesOrDivide && (needed == Times || needed == Divide)))
                {
                    n++;
                }
            }

            return n;
        }

        private static ScoredMove PickMedium(List<ScoredMove> candidates, int choiceSeed)
        {
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            int count = candidates.Count;
            int lo = (count * 35) / 100;
            int hi = System.Math.Max(lo + 1, (count * 75) / 100);
            if (hi > count) hi = count;
            if (lo >= hi) lo = 0;

            var rng = new DeterministicRandom(choiceSeed);
            return candidates[lo + rng.NextInt(hi - lo)];
        }

        private static string Key(byte[] ids)
        {
            var chars = new char[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                chars[i] = (char)('A' + ids[i]);
            return new string(chars);
        }

        private sealed class ScoredMove
        {
            public int Score;
            public List<TilePlacement> Placements;
        }
    }
}
