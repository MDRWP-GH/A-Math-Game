using System;
using System.Collections.Generic;
using AMath.Art;
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
        internal const float CellGap = 2f;

        private readonly Button[,] _cells = new Button[GameRules.BoardSize, GameRules.BoardSize];
        private readonly Text[,] _labels = new Text[GameRules.BoardSize, GameRules.BoardSize];
        private readonly Image[,] _tileIcons = new Image[GameRules.BoardSize, GameRules.BoardSize];
        private readonly byte?[,] _displayedTileIds = new byte?[GameRules.BoardSize, GameRules.BoardSize];
        private readonly Action<byte> _onTileClicked;
        private readonly Action _onEmptyCellClicked;
        private readonly float _cellSize;
        private readonly HashSet<int> _guideCells = new();
        private int _hoverX = -1;
        private int _hoverY = -1;

        /// <summary>Builds the cell grid under <paramref name="parent"/>.</summary>
        public MatchBoardView(
            UiFactory ui,
            RectTransform parent,
            Action<int, int> onCellClicked,
            float cellSize = CellSize,
            Action<Button, int, int> configureCell = null,
            Action<byte> onTileClicked = null,
            Action onEmptyCellClicked = null)
        {
            _cellSize = cellSize;
            _onTileClicked = onTileClicked;
            _onEmptyCellClicked = onEmptyCellClicked;
            float origin = -((GameRules.BoardSize - 1) * _cellSize) * 0.5f;
            float innerSize = _cellSize - CellGap;
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    int cellX = x;
                    int cellY = y;
                    Button button = ui.CreateButton(
                        parent, $"C{x}_{y}", string.Empty,
                        UiPalette.CellPlain, UiPalette.CellHighlight,
                        () =>
                        {
                            byte? displayed = _displayedTileIds[cellX, cellY];
                            if (displayed.HasValue)
                                _onTileClicked?.Invoke(displayed.Value);
                            else
                                _onEmptyCellClicked?.Invoke();
                            onCellClicked(cellX, cellY);
                        }, 11);

                    UiFactory.SetCenteredRect(
                        button.GetComponent<RectTransform>(),
                        new Vector2(origin + x * _cellSize, -origin - y * _cellSize),
                        new Vector2(innerSize, innerSize));

                    UiFactory.AddOutline(button.gameObject, UiPalette.FieldBorder, new Vector2(1f, -1f));

                    Text label = button.GetComponentInChildren<Text>();
                    if (label != null)
                    {
                        label.horizontalOverflow = HorizontalWrapMode.Wrap;
                        label.verticalOverflow = VerticalWrapMode.Overflow;
                        label.alignment = TextAnchor.MiddleCenter;
                        label.color = Color.white;
                    }

                    Image tileIcon = UiFactory.CreateImage("TileIcon", button.transform, Color.clear);
                    tileIcon.raycastTarget = false;
                    tileIcon.preserveAspect = true;
                    // Tile sprites already contain a consistent transparent margin.
                    // Filling the cell avoids applying that padding twice while the
                    // two-pixel gutter still keeps neighbouring tiles separate.
                    UiFactory.Stretch(tileIcon.rectTransform);

                    _cells[x, y] = button;
                    _labels[x, y] = label;
                    _tileIcons[x, y] = tileIcon;
                    configureCell?.Invoke(button, x, y);
                }
            }
        }

        /// <summary>Highlights a cell while a tile is selected or being dragged.</summary>
        public void SetHoverCell(int x, int y)
        {
            if (_hoverX == x && _hoverY == y) return;
            _hoverX = x;
            _hoverY = y;
        }

        /// <summary>Clears the hover highlight.</summary>
        public void ClearHover()
        {
            _hoverX = -1;
            _hoverY = -1;
        }

        /// <summary>Returns the rect for a built cell, or null when out of range.</summary>
        public RectTransform GetCellRect(int x, int y)
        {
            if (x < 0 || y < 0 || x >= GameRules.BoardSize || y >= GameRules.BoardSize)
                return null;

            return _cells[x, y]?.GetComponent<RectTransform>();
        }

        /// <summary>Runs a callback for every built cell (e.g. attach drag targets).</summary>
        public void ConfigureCells(Action<Button, int, int> configureCell)
        {
            if (configureCell == null) return;
            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                    configureCell(_cells[x, y], x, y);
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
                    ResolveCell(grid, x, y, out byte? tileId, out string symbol, out Color color, out int fontSize);
                    ApplyDraft(input, x, y, ref tileId, ref symbol, ref color, ref fontSize);
                    _displayedTileIds[x, y] = ResolvePhysicalTileId(grid, input, x, y);

                    bool showIcon = tileId.HasValue;
                    Image icon = _tileIcons[x, y];
                    if (showIcon)
                    {
                        Sprite sprite = TileIcons.ForTile(tileId.Value);
                        icon.sprite = sprite;
                        icon.color = sprite != null ? Color.white : Color.clear;
                        icon.enabled = sprite != null;
                        _labels[x, y].text = sprite != null ? string.Empty : symbol;
                    }
                    else
                    {
                        icon.sprite = null;
                        icon.enabled = false;
                        _labels[x, y].text = symbol;
                    }

                    _labels[x, y].fontSize = fontSize;
                    _cells[x, y].targetGraphic.color = ApplyHover(color, x, y);
                }
            }
        }

        private static byte? ResolvePhysicalTileId(BoardGrid grid, TurnInputSession input, int x, int y)
        {
            if (input != null)
            {
                for (int i = 0; i < input.PendingPlacements.Count; i++)
                {
                    TilePlacement placement = input.PendingPlacements[i];
                    if (placement.X == x && placement.Y == y)
                        return placement.TileId;
                }
            }

            return grid != null && grid.IsOccupied(x, y) ? grid.CellAt(x, y).TileId : null;
        }

        private Color ApplyHover(Color baseColor, int x, int y)
        {
            if (_hoverX != x || _hoverY != y)
                return baseColor;

            return Color.Lerp(baseColor, UiPalette.CellHover, 0.55f);
        }

        private void ResolveCell(
            BoardGrid grid, int x, int y,
            out byte? tileId, out string symbol, out Color color, out int fontSize)
        {
            tileId = null;
            fontSize = 18;
            if (grid != null && grid.IsOccupied(x, y))
            {
                tileId = grid.CellAt(x, y).EffectiveTileId;
                symbol = SymbolOf(tileId.Value);
                color = UiPalette.CellOccupied;
                return;
            }

            symbol = string.Empty;
            color = UiPalette.CellPlain;

            PremiumType premium = BoardGrid.PremiumAt(x, y);
            if (premium != PremiumType.None)
            {
                symbol = ShortPremiumHint(premium);
                color = PremiumColor(premium);
                fontSize = 12;
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
                return UiPalette.CellGuide;

            return Color.Lerp(baseColor, UiPalette.CellGuide, 0.55f);
        }

        private static void ApplyDraft(
            TurnInputSession input, int x, int y,
            ref byte? tileId, ref string symbol, ref Color color, ref int fontSize)
        {
            if (input == null) return;

            for (int i = 0; i < input.PendingPlacements.Count; i++)
            {
                TilePlacement placement = input.PendingPlacements[i];
                if (placement.X != x || placement.Y != y) continue;

                tileId = placement.EffectiveTileId;
                symbol = SymbolOf(tileId.Value);
                color = input.PreviewValidation is { IsValid: true }
                    ? UiPalette.CellDraftValid
                    : UiPalette.CellDraftInvalid;
                fontSize = 18;
            }
        }

        private static string ShortPremiumHint(PremiumType premium) => premium switch
        {
            PremiumType.TileX2 => "×2\nT",
            PremiumType.TileX3 => "×3\nT",
            PremiumType.EquationX2 => "×2\nE",
            PremiumType.EquationX3 => "×3\nE",
            _ => string.Empty
        };

        private static Color PremiumColor(PremiumType premium) => premium switch
        {
            PremiumType.TileX2 => new Color(0.95f, 0.55f, 0.18f, 1f),      // orange — ×2 piece
            PremiumType.TileX3 => new Color(0.28f, 0.62f, 0.88f, 1f),      // blue — ×3 piece
            PremiumType.EquationX2 => new Color(0.95f, 0.82f, 0.22f, 1f),  // yellow — ×2 equation
            PremiumType.EquationX3 => new Color(0.88f, 0.28f, 0.28f, 1f),  // red — ×3 equation
            _ => UiPalette.CellPlain
        };
    }
}
