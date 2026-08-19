using System;
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
    /// Split out of <see cref="PlaySessionController"/> because it is the one
    /// part of that screen that touches all 225 cells on every refresh, and
    /// keeping its colours and premium legend next to the loop that uses them
    /// makes both easier to reason about.
    /// </summary>
    internal sealed class MatchBoardView
    {
        private const float CellSize = 46f;

        private static readonly Color CellPlain = new(0.16f, 0.22f, 0.40f, 1f);
        private static readonly Color CellOccupied = new(0.20f, 0.45f, 0.55f, 1f);
        private static readonly Color CellDraftValid = new(0.20f, 0.55f, 0.30f, 1f);
        private static readonly Color CellDraftInvalid = new(0.65f, 0.30f, 0.25f, 1f);
        private static readonly Color CellHighlight = new(0.25f, 0.35f, 0.55f, 1f);

        private readonly Button[,] _cells = new Button[GameRules.BoardSize, GameRules.BoardSize];
        private readonly Text[,] _labels = new Text[GameRules.BoardSize, GameRules.BoardSize];

        /// <summary>Builds the cell grid under <paramref name="parent"/>.</summary>
        public MatchBoardView(UiFactory ui, RectTransform parent, Action<int, int> onCellClicked)
        {
            float origin = -((GameRules.BoardSize - 1) * CellSize) * 0.5f;
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    int cellX = x;
                    int cellY = y;
                    Button button = ui.CreateButton(
                        parent, $"C{x}_{y}", string.Empty,
                        CellPlain, CellHighlight,
                        () => onCellClicked(cellX, cellY), 16);

                    UiFactory.SetCenteredRect(
                        button.GetComponent<RectTransform>(),
                        new Vector2(origin + x * CellSize, -origin - y * CellSize),
                        new Vector2(CellSize - 2f, CellSize - 2f));

                    _cells[x, y] = button;
                    _labels[x, y] = button.GetComponentInChildren<Text>();
                }
            }
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
                    ResolveCell(grid, x, y, out string symbol, out Color color);
                    ApplyDraft(input, x, y, ref symbol, ref color);

                    _labels[x, y].text = symbol;
                    _cells[x, y].targetGraphic.color = color;
                }
            }
        }

        private static void ResolveCell(BoardGrid grid, int x, int y, out string symbol, out Color color)
        {
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
            }

            if (x == GameRules.CenterX && y == GameRules.CenterY)
                symbol = "★";
        }

        private static void ApplyDraft(
            TurnInputSession input, int x, int y, ref string symbol, ref Color color)
        {
            if (input == null) return;

            for (int i = 0; i < input.PendingPlacements.Count; i++)
            {
                TilePlacement placement = input.PendingPlacements[i];
                if (placement.X != x || placement.Y != y) continue;

                symbol = SymbolOf(placement.EffectiveTileId);
                color = input.PreviewValidation is { IsValid: true } ? CellDraftValid : CellDraftInvalid;
            }
        }

        private static Color PremiumColor(PremiumType premium) => premium switch
        {
            PremiumType.TileX2 => new Color(0.82f, 0.49f, 0.18f, 1f),      // orange — ×2 tile
            PremiumType.TileX3 => new Color(0.18f, 0.42f, 0.76f, 1f),      // blue — ×3 tile
            PremiumType.EquationX2 => new Color(0.76f, 0.63f, 0.16f, 1f),  // yellow — ×2 equation
            PremiumType.EquationX3 => new Color(0.72f, 0.22f, 0.22f, 1f),  // red — ×3 equation
            _ => CellPlain
        };
    }
}
