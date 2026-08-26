using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using UnityEngine;
using UnityEngine.UI;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.UI
{
    /// <summary>
    /// The 15x15 match board: builds the cell buttons once, then repaints them
    /// from the authoritative grid plus the local draft.
    ///
    /// Colours follow the in-match mockup — warm wood cells with bright
    /// premium squares — so the board reads clearly on the forest backdrop.
    /// </summary>
    internal sealed class MatchBoardView
    {
        private const float CellSize = 46f;
        private static readonly Color CellGuide = new(0.98f, 0.84f, 0.18f, 0.95f);

        private static readonly Color CellPlain = new(0.55f, 0.38f, 0.22f, 1f);
        private static readonly Color CellOccupied = new(0.92f, 0.55f, 0.18f, 1f);
        private static readonly Color CellDraftValid = new(0.28f, 0.72f, 0.38f, 1f);
        private static readonly Color CellDraftInvalid = new(0.78f, 0.28f, 0.24f, 1f);
        private static readonly Color CellHighlight = new(0.70f, 0.52f, 0.32f, 1f);

        private readonly Button[,] _cells = new Button[GameRules.BoardSize, GameRules.BoardSize];
        private readonly Text[,] _labels = new Text[GameRules.BoardSize, GameRules.BoardSize];
        private readonly float _cellSize;
        private readonly HashSet<int> _guideCells = new();

        /// <summary>Builds the cell grid under <paramref name="parent"/>.</summary>
        public MatchBoardView(UiFactory ui, RectTransform parent, Action<int, int> onCellClicked, float cellSize = CellSize)
        {
            _cellSize = cellSize;
            float origin = -((GameRules.BoardSize - 1) * _cellSize) * 0.5f;
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    int cellX = x;
                    int cellY = y;
                    Button button = ui.CreateButton(
                        parent, $"C{x}_{y}", string.Empty,
                        CellPlain, CellHighlight,
                        () => onCellClicked(cellX, cellY), 11);

                    UiFactory.SetCenteredRect(
                        button.GetComponent<RectTransform>(),
                        new Vector2(origin + x * _cellSize, -origin - y * _cellSize),
                        new Vector2(_cellSize - 2f, _cellSize - 2f));

                    Text label = button.GetComponentInChildren<Text>();
                    if (label != null)
                    {
                        label.horizontalOverflow = HorizontalWrapMode.Wrap;
                        label.verticalOverflow = VerticalWrapMode.Overflow;
                        label.alignment = TextAnchor.MiddleCenter;
                        label.color = Color.white;
                    }

                    _cells[x, y] = button;
                    _labels[x, y] = label;
                }
            }
        }

        /// <summary>Marks cells the tutorial wants the player to occupy next.</summary>
        public void SetGuideCells(IReadOnlyList<TilePlacement> placements)
        {
            _guideCells.Clear();
            if (placements == null) return;
            for (int i = 0; i < placements.Count; i++)
                _guideCells.Add(placements[i].Y * GameRules.BoardSize + placements[i].X);
        }

        /// <summary>
        /// Repaints every cell. <paramref name="grid"/> may be null before a
        /// match starts, and <paramref name="input"/> may be null when the
        /// local player has no draft in progress.
        /// </summary>
        public void Refresh(BoardGrid grid, TurnInputSession input)
        {
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    ResolveCell(grid, x, y, out string symbol, out Color color, out int fontSize);
                    ApplyDraft(input, x, y, ref symbol, ref color, ref fontSize);

                    _labels[x, y].text = symbol;
                    _labels[x, y].fontSize = fontSize;
                    _cells[x, y].targetGraphic.color = color;
                }
            }
        }

        private void ResolveCell(
            BoardGrid grid, int x, int y, out string symbol, out Color color, out int fontSize)
        {
            fontSize = 18;
            if (grid != null && grid.IsOccupied(x, y))
            {
                symbol = SymbolOf(grid.CellAt(x, y).EffectiveTileId);
                color = CellOccupied;
                return;
            }

            symbol = string.Empty;
            color = CellPlain;

            PremiumType premium = BoardGrid.PremiumAt(x, y);
            if (premium != PremiumType.None)
            {
                symbol = PlacementPreviewFormatter.PremiumHint(premium);
                color = PremiumColor(premium);
                fontSize = 9;
            }

            if (x == GameRules.CenterX && y == GameRules.CenterY)
            {
                symbol = "★";
                fontSize = 20;
            }

            if (_guideCells.Contains(y * GameRules.BoardSize + x))
                color = TintGuide(color, premium);
        }

        /// <summary>
        /// Plain guide cells stay gold. Premium cells keep their bonus hue
        /// and only take a gold wash, so ×2/×3 squares remain readable.
        /// </summary>
        private static Color TintGuide(Color baseColor, PremiumType premium)
        {
            if (premium == PremiumType.None)
                return CellGuide;

            return Color.Lerp(baseColor, CellGuide, 0.4f);
        }

        private static void ApplyDraft(
            TurnInputSession input, int x, int y,
            ref string symbol, ref Color color, ref int fontSize)
        {
            if (input == null) return;

            for (int i = 0; i < input.PendingPlacements.Count; i++)
            {
                TilePlacement placement = input.PendingPlacements[i];
                if (placement.X != x || placement.Y != y) continue;

                symbol = SymbolOf(placement.EffectiveTileId);
                color = input.PreviewValidation is { IsValid: true } ? CellDraftValid : CellDraftInvalid;
                fontSize = 18;
            }
        }

        private static Color PremiumColor(PremiumType premium) => premium switch
        {
            PremiumType.TileX2 => new Color(0.95f, 0.55f, 0.18f, 1f),      // orange — ×2 piece
            PremiumType.TileX3 => new Color(0.28f, 0.62f, 0.88f, 1f),      // blue — ×3 piece
            PremiumType.EquationX2 => new Color(0.95f, 0.82f, 0.22f, 1f),  // yellow — ×2 equation
            PremiumType.EquationX3 => new Color(0.88f, 0.28f, 0.28f, 1f),  // red — ×3 equation
            _ => CellPlain
        };
    }
}
