using System;
using System.Collections.Generic;
using System.Linq;
using AMath.Art;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Scripted;
using UnityEngine;
using UnityEngine.UI;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.UI.Tutorial
{
    /// <summary>
    /// Scripted match rendered with the same HUD geometry and controls as a
    /// normal match. Only the enabled actions differ by lesson step.
    /// </summary>
    internal sealed class TutorialMatchView
    {
        private const float RackPitch = MatchHudLayout.RackPitch;

        private readonly UiFactory _ui;
        private readonly ILocalizedTextProvider _text;
        private readonly TutorialMatchHost _host;
        private readonly MatchBoardView _boardView;
        private readonly TilePreviewPanel _tilePreviewPanel;
        private readonly TilePlacementInput _tilePlacementInput;
        private readonly Transform _rackRoot;
        private readonly Text _statusText;
        private readonly Text _previewText;
        private readonly Text _timerText;
        private readonly Text _bagLabel;
        private readonly RectTransform _boardRoot;
        private readonly MatchHudElements.Seat[] _seats = new MatchHudElements.Seat[MatchHudLayout.VisibleSeatCount];
        private readonly MatchCommandDrawerAnimator _commandDrawer;
        private readonly Button _commandToggle;
        private readonly Text _commandToggleLabel;
        private readonly Button _confirmButton;
        private readonly Button _clearButton;
        private readonly Button _passButton;
        private readonly Button _exchangeButton;
        private readonly List<Button> _rackButtons = new(GameRules.RackSize);
        private readonly List<Text> _rackLabels = new(GameRules.RackSize);
        private readonly List<Image> _rackImages = new(GameRules.RackSize);
        private readonly List<int> _exchangeSelection = new(2);
        private readonly Action<string> _correction;
        private bool _exchangeMode;

        public TutorialMatchView(
            UiFactory ui,
            ILocalizedTextProvider text,
            TutorialMatchHost host,
            Transform canvas,
            Action<string> correction = null,
            Action onMenu = null)
        {
            _ui = ui;
            _text = text;
            _host = host;
            _correction = correction;

            Image background = UiFactory.CreateFullScreenBackground(canvas, "In Game Backgrounds", UiPalette.Background);
            background.raycastTarget = true;
            background.gameObject.AddComponent<TilePreviewBackground>()
                .Configure(() => _tilePreviewPanel.Hide());
            _tilePreviewPanel = new TilePreviewPanel(
                _ui, _text, canvas, MatchHudLayout.Standard.TilePreview);

            _statusText = _ui.CreateText(
                "Status", canvas, string.Empty, 26, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            MatchHudLayout.Standard.Status.Apply(_statusText.rectTransform);
            UiFactory.AddDoubleOutline(_statusText.gameObject, new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));

            MatchHudElements.Timer timer = MatchHudElements.CreateTimer(_ui, canvas,
                _text.GetText("tutorial.ui.time"), _text.GetText("tutorial.ui.no_limit"));
            _timerText = timer.Value;

            Button menuButton = _ui.CreateAccentButton(canvas, "Menu",
                _text.GetText("tutorial.ui.menu"), UiPalette.Secondary, UiPalette.SecondaryHighlight,
                onMenu ?? (() => { }), 20);
            UiFactory.SetAnchoredRect(menuButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(120f, 42f), new Vector2(-420f, -34f));

            for (int i = 0; i < _seats.Length; i++)
                _seats[i] = MatchHudElements.CreateSeat(_ui, canvas, i);

            _bagLabel = MatchHudElements.CreateBag(_ui, canvas);

            Image boardFrame = MatchHudElements.CreateBoardFrame(canvas);
            _boardRoot = UiFactory.CreateRect("Board", boardFrame.transform);
            UiFactory.SetCenteredRect(_boardRoot, Vector2.zero, MatchHudLayout.Standard.BoardSize);
            _boardView = new MatchBoardView(
                _ui, _boardRoot, OnCellClicked, MatchHudLayout.Standard.BoardCellSize,
                onTileClicked: tileId => _tilePreviewPanel.Show(tileId),
                onEmptyCellClicked: () => _tilePreviewPanel.Hide());
            _tilePlacementInput = new TilePlacementInput(
                _boardView,
                () => _host.IsPlayerInputEnabled,
                () => _host.Input.SelectedRackIndex,
                PlaceFromRack,
                RefreshBoardVisual);
            _boardView.ConfigureCells(_tilePlacementInput.AttachBoardCell);

            Image rackShelf = MatchHudElements.CreateRackShelf(canvas);
            _rackRoot = UiFactory.CreateRect("Rack", rackShelf.transform).transform;
            UiFactory.Stretch((RectTransform)_rackRoot);

            _previewText = _ui.CreateText("Preview", canvas, string.Empty, 16, FontStyle.Normal,
                Color.white, TextAnchor.MiddleCenter);
            _previewText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _previewText.verticalOverflow = VerticalWrapMode.Overflow;
            MatchHudLayout.Standard.Preview.Apply(_previewText.rectTransform);
            UiFactory.AddOutline(_previewText.gameObject, Color.black, new Vector2(1.5f, -1.5f));

            RectTransform commandRoot = UiFactory.CreateRect("Command Drawer", canvas);
            MatchHudLayout.Standard.CommandDrawer.Apply(commandRoot);
            Image commandPanel = UiFactory.CreateGlassPanel(commandRoot, "Panel",
                new Color(UiPalette.GlassStrong.r, UiPalette.GlassStrong.g, UiPalette.GlassStrong.b, 0.94f));
            UiFactory.Stretch(commandPanel.rectTransform);
            UiFactory.AddOutline(commandPanel.gameObject, new Color(1f, 1f, 1f, 0.16f), new Vector2(1.5f, -1.5f));
            _confirmButton = MatchHudElements.CreateAction(_ui, commandRoot, "Confirm",
                _text.GetText("tutorial.ui.confirm"), UiPalette.Success, UiPalette.SuccessHighlight, Confirm, 0);
            _clearButton = MatchHudElements.CreateAction(_ui, commandRoot, "Clear",
                _text.GetText("tutorial.ui.clear"), UiPalette.Secondary, UiPalette.SecondaryHighlight, ClearDraft, 1);
            _passButton = MatchHudElements.CreateAction(_ui, commandRoot, "Pass",
                _text.GetText("tutorial.ui.pass"), UiPalette.Secondary, UiPalette.SecondaryHighlight, Pass, 2);
            _exchangeButton = MatchHudElements.CreateAction(_ui, commandRoot, "Exchange",
                _text.GetText("tutorial.ui.exchange"), UiPalette.Primary, UiPalette.PrimaryHighlight, Exchange, 3);
            _commandDrawer = commandRoot.gameObject.AddComponent<MatchCommandDrawerAnimator>();
            _commandDrawer.Configure(MatchHudLayout.Standard.CommandDrawer.Position,
                MatchHudLayout.Standard.CommandDrawerCollapsedPosition, 0.30f);
            _commandDrawer.SetExpandedInstant(false);
            _commandToggle = _ui.CreateAccentButton(canvas, "Command Drawer Toggle", string.Empty,
                UiPalette.Primary, UiPalette.PrimaryHighlight, ToggleCommands, 18);
            MatchHudLayout.Standard.CommandToggle.Apply(_commandToggle.GetComponent<RectTransform>());
            _commandToggleLabel = _commandToggle.GetComponentInChildren<Text>();
            UpdateCommandToggleLabel();

            _host.Changed += Refresh;
            Refresh();
        }

        /// <summary>Registers UI rects used by the Master Duel-style spotlight.</summary>
        public void RegisterSpotlightTargets(TutorialHudPresenter presenter)
        {
            presenter.RegisterSpotlightTarget(IntroTutorialSequence.DemoBoardTargetId, _boardRoot);
            presenter.RegisterSpotlightTarget(IntroTutorialSequence.DemoRackTargetId, (RectTransform)_rackRoot);
            presenter.RegisterSpotlightTargetResolver(
                IntroTutorialSequence.DemoGuidedTileTargetId, ResolveGuidedRackTile);

            for (int y = 0; y < GameRules.BoardSize; y++)
            {
                for (int x = 0; x < GameRules.BoardSize; x++)
                {
                    RectTransform cell = _boardView.GetCellRect(x, y);
                    if (cell != null)
                        presenter.RegisterSpotlightTarget(IntroTutorialSequence.CellTargetId(x, y), cell);
                }
            }

            presenter.RegisterSpotlightTargetResolver(
                IntroTutorialSequence.DemoConfirmTargetId,
                () => ResolveCommandTarget(_confirmButton));
            presenter.RegisterSpotlightTargetResolver(
                IntroTutorialSequence.DemoPassTargetId,
                () => ResolveCommandTarget(_passButton));
            presenter.RegisterSpotlightTargetResolver(
                IntroTutorialSequence.DemoExchangeTargetId,
                ResolveExchangeTarget);
        }

        /// <summary>Repaints board, rack and turn status.</summary>
        public void Refresh()
        {
            bool placementTurn = _host.IsPlayerInputEnabled;
            bool passTurn = _host.IsPassActionEnabled;
            bool exchangeTurn = _host.IsExchangeActionEnabled;
            string status = passTurn
                ? _text.GetText("tutorial.ui.pass_turn")
                : exchangeTurn
                    ? _text.GetText("tutorial.ui.exchange_turn")
                    : placementTurn
                        ? _text.GetText("tutorial.ui.your_turn")
                        : _text.GetText("tutorial.ui.wait_script");
            _statusText.text = $"{_text.GetText("tutorial.ui.turn")} {_host.Turns.TurnNumber}  •  {status}";
            _timerText.text = _text.GetText("tutorial.ui.no_limit");
            _bagLabel.text = _host.Game.BagCount.ToString();
            RefreshSeats();

            _boardView.SetGuideCells(_host.RemainingGuidePlacements);
            _boardView.Refresh(_host.Board.Grid, _host.Input);

            _confirmButton.interactable = placementTurn && !_exchangeMode;
            _clearButton.interactable = false;
            _passButton.interactable = passTurn;
            _exchangeButton.interactable = exchangeTurn;
            if (!exchangeTurn)
            {
                _exchangeMode = false;
                _exchangeSelection.Clear();
            }
            ColorBlock exchangeColors = _exchangeButton.colors;
            exchangeColors.normalColor = _exchangeMode ? UiPalette.PrimaryHighlight : UiPalette.Primary;
            _exchangeButton.colors = exchangeColors;
            if (_host.Input.PendingPlacements.Count > 0)
                _previewText.text = PlacementPreviewFormatter.FormatPreview(
                    _host.Input.PreviewValidation, _host.Input.PreviewBreakdown);
            else if (exchangeTurn)
                _previewText.text = _text.GetText(!_exchangeMode
                    ? "tutorial.ui.exchange_arm"
                    : _exchangeSelection.Count == 0
                        ? "tutorial.ui.exchange_first"
                        : _exchangeSelection.Count < _host.GuidedExchangeIndices.Count
                            ? "tutorial.ui.exchange_second"
                            : "tutorial.ui.exchange_commit");
            else
                _previewText.text = _text.GetText("tutorial.ui.idle_hint");
            RefreshRack(placementTurn || (exchangeTurn && _exchangeMode));
        }

        private void RefreshSeats()
        {
            IReadOnlyList<PlayerState> players = _host.Players.Players;
            PlayerState local = _host.Players.GetById(_host.Players.LocalPlayerId);
            int slot = 0;
            if (local != null)
                PaintSeat(_seats[slot++], local, 1);
            for (int i = 0; i < players.Count && slot < _seats.Length; i++)
            {
                if (local != null && players[i].PlayerId == local.PlayerId) continue;
                PaintSeat(_seats[slot], players[i], slot + 1);
                slot++;
            }
            for (; slot < _seats.Length; slot++)
                _seats[slot].Root.SetActive(false);
        }

        private void PaintSeat(MatchHudElements.Seat seat, PlayerState player, int number)
        {
            seat.Root.SetActive(true);
            seat.Label.text = $"P{number}";
            string you = player.PlayerId == _host.Players.LocalPlayerId
                ? $" ({_text.GetText("tutorial.ui.you")})" : string.Empty;
            seat.Score.text = $"{_text.GetText("tutorial.ui.score")} {player.Score}{you}";
            seat.Avatar.color = PlayerColorPalette.ColorOf(player.ColorId);
            seat.Label.color = MatchHudElements.ContrastingTextColor(seat.Avatar.color);
            Color ring = UiPalette.TurnRing;
            ring.a = player.PlayerId == _host.Turns.CurrentPlayerId ? 0.95f : 0f;
            seat.TurnRing.color = ring;
        }

        public void Dispose()
        {
            _host.Changed -= Refresh;
        }

        public void HideTilePreview() => _tilePreviewPanel.Hide();

        public void ResetForChapter()
        {
            _tilePreviewPanel.Hide();
            _exchangeMode = false;
            _exchangeSelection.Clear();
            _commandDrawer.SetExpandedInstant(false);
            UpdateCommandToggleLabel();
        }

        private void RefreshRack(bool rackActive)
        {
            PlayerState local = _host.Players.GetById(_host.Players.LocalPlayerId);
            int tileCount = local?.Rack.Count ?? 0;
            EnsureRackButtons(tileCount);

            HashSet<int> needed = NeededRackIndices(local, _host.GuidedRackTileId, _host.GuidedExchangeIndices);
            float start = -((tileCount - 1) * RackPitch) * 0.5f;
            for (int i = 0; i < _rackButtons.Count; i++)
            {
                Button button = _rackButtons[i];
                if (i >= tileCount)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                byte tileId = local.Rack[i];
                bool selected = _host.Input.SelectedRackIndex == i || _exchangeSelection.Contains(i);
                bool guide = needed.Contains(i);
                button.gameObject.SetActive(true);
                button.interactable = rackActive;
                Sprite sprite = TileIcons.ForTile(tileId);
                Image image = _rackImages[i];
                Text label = _rackLabels[i];
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.color = selected ? UiPalette.CellSelected : guide ? UiPalette.CellGuide : Color.white;
                    ColorBlock colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.pressedColor = new Color(0.90f, 0.85f, 0.70f, 1f);
                    button.colors = colors;
                    label.text = string.Empty;
                }
                else
                {
                    image.sprite = null;
                    image.preserveAspect = false;
                    image.color = Color.white;
                    ColorBlock colors = button.colors;
                    colors.normalColor = selected ? UiPalette.Primary : guide ? UiPalette.CellGuide : UiPalette.CellOccupied;
                    colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, 0.16f);
                    button.colors = colors;
                    label.text = $"{SymbolOf(tileId)}\n{PointsOf(tileId)}";
                }
                var rackRect = button.GetComponent<RectTransform>();
                UiFactory.SetCenteredRect(
                    rackRect,
                    new Vector2(start + i * RackPitch, 0f),
                    new Vector2(70f, 84f));
                rackRect.localScale = Vector3.one;

                AttachTutorialRackButton(button, i);
            }
        }

        private void RefreshBoardVisual() =>
            _boardView.Refresh(_host.Board.Grid, _host.Input);

        private static HashSet<int> NeededRackIndices(
            PlayerState local,
            byte? guidedTileId,
            IReadOnlyList<int> guidedExchangeIndices)
        {
            var needed = new HashSet<int>();
            if (local == null)
                return needed;

            if (guidedExchangeIndices != null && guidedExchangeIndices.Count > 0)
            {
                for (int i = 0; i < guidedExchangeIndices.Count; i++)
                    needed.Add(guidedExchangeIndices[i]);
                return needed;
            }

            if (guidedTileId.HasValue)
            {
                for (int i = 0; i < local.Rack.Count; i++)
                {
                    if (local.Rack[i] == guidedTileId.Value)
                    {
                        needed.Add(i);
                        return needed;
                    }
                }
            }

            return needed;
        }

        private void EnsureRackButtons(int required)
        {
            while (_rackButtons.Count < required)
            {
                int index = _rackButtons.Count;
                Button button = UiFactory.CreateIconButton(
                    _rackRoot, $"R{index}", null, UiPalette.CellOccupied,
                    () => OnRackClicked(index));
                Text label = _ui.CreateText("Label", button.transform, string.Empty,
                    18, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(label.rectTransform);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                _rackButtons.Add(button);
                _rackLabels.Add(label);
                _rackImages.Add(button.GetComponent<Image>());
                button.gameObject.AddComponent<TilePreviewRackTarget>().Configure(
                    () =>
                    {
                        PlayerState owner = _host.Players.GetById(_host.Players.LocalPlayerId);
                        return owner != null && index < owner.Rack.Count
                            ? (byte?)owner.Rack[index]
                            : null;
                    },
                    tileId => _tilePreviewPanel.Show(tileId));
                AttachTutorialRackButton(button, index);
            }
        }

        private void AttachTutorialRackButton(Button button, int rackIndex)
        {
            _tilePlacementInput.AttachRackButton(
                button,
                rackIndex,
                () =>
                {
                    PlayerState rackOwner = _host.Players.GetById(_host.Players.LocalPlayerId);
                    if (rackOwner == null || rackIndex >= rackOwner.Rack.Count)
                        return null;
                    return TileIcons.ForTile(rackOwner.Rack[rackIndex]);
                },
                OnRackDragStarted);
        }

        private void OnRackDragStarted(int rackIndex)
        {
            if (!_host.IsPlayerInputEnabled) return;
            if (!IsGuidedRackChoice(rackIndex))
            {
                ShowCorrection(_text.GetText("tutorial.error.off_script_tile"));
                return;
            }
            _host.Input.SelectFromRack(rackIndex);
        }

        private void PlaceFromRack(int rackIndex, int x, int y)
        {
            if (!_host.IsPlayerInputEnabled) return;
            if (!IsGuidedRackChoice(rackIndex))
            {
                ShowCorrection(_text.GetText("tutorial.error.off_script_tile"));
                return;
            }

            TurnInputSession input = _host.Input;
            for (int i = 0; i < input.PendingPlacements.Count; i++)
            {
                if (input.PendingPlacements[i].X == x && input.PendingPlacements[i].Y == y)
                {
                    ShowCorrection(_text.GetText("tutorial.error.keep_placed"));
                    return;
                }
            }

            input.SelectFromRack(rackIndex);
            if (!input.TryPlaceOnCell(x, y, out string error) && !string.IsNullOrEmpty(error))
                ShowCorrection(error);
        }

        private void OnRackClicked(int index)
        {
            if (_host.IsExchangeActionEnabled)
            {
                if (!_exchangeMode) return;
                IReadOnlyList<int> required = _host.GuidedExchangeIndices;
                if (required != null && required.Count > 0 && !required.Contains(index))
                {
                    ShowCorrection(_text.GetText("tutorial.error.exchange_selection"));
                    return;
                }
                if (!_exchangeSelection.Remove(index))
                    _exchangeSelection.Add(index);
                Refresh();
                return;
            }

            if (!_host.IsPlayerInputEnabled) return;
            if (!IsGuidedRackChoice(index))
            {
                ShowCorrection(_text.GetText("tutorial.error.off_script_tile"));
                return;
            }
            _host.Input.SelectFromRack(index);
        }

        private void OnCellClicked(int x, int y)
        {
            if (_host.Board.Grid.IsOccupied(x, y)) return;

            TurnInputSession input = _host.Input;
            for (int i = 0; i < input.PendingPlacements.Count; i++)
            {
                if (input.PendingPlacements[i].X == x && input.PendingPlacements[i].Y == y)
                    return;
            }

            if (!_host.IsPlayerInputEnabled) return;
            if (!input.SelectedRackIndex.HasValue)
            {
                ShowCorrection(_text.GetText("tutorial.error.select_tile_first"));
                return;
            }
            if (!input.TryPlaceOnCell(x, y, out string error) && !string.IsNullOrEmpty(error))
                ShowCorrection(error);
        }

        private void Confirm()
        {
            if (!_host.Input.TryConfirmPlace(out string error) && !string.IsNullOrEmpty(error))
                ShowCorrection(error);
        }

        private void ClearDraft() => _host.Input.ClearDraft();

        private void Pass()
        {
            if (!_host.IsPassActionEnabled) return;
            _host.Input.RequestPass();
        }

        private void Exchange()
        {
            if (!_host.IsExchangeActionEnabled) return;
            if (!_exchangeMode)
            {
                _exchangeMode = true;
                Refresh();
                return;
            }
            if (_exchangeSelection.Count == 0)
            {
                _exchangeMode = false;
                Refresh();
                return;
            }
            if (!_host.Input.TryRequestExchange(_exchangeSelection, out string error))
            {
                if (!string.IsNullOrEmpty(error)) ShowCorrection(error);
                return;
            }
            _exchangeMode = false;
            _exchangeSelection.Clear();
            Refresh();
        }

        private void ToggleCommands()
        {
            _commandDrawer.Toggle();
            UpdateCommandToggleLabel();
            if (_commandDrawer.IsExpanded)
                UiFactory.SelectFirstInteractable(_confirmButton, _clearButton, _passButton, _exchangeButton);
            else
                UiFactory.SelectFirstInteractable(_commandToggle);
        }

        private void UpdateCommandToggleLabel()
        {
            _commandToggleLabel.text = _text.GetText(_commandDrawer.IsExpanded
                ? "tutorial.ui.commands_hide" : "tutorial.ui.commands_open");
        }

        private RectTransform ResolveCommandTarget(Button button) =>
            _commandDrawer.IsExpanded && !_commandDrawer.IsAnimating
                ? button.GetComponent<RectTransform>()
                : _commandToggle.GetComponent<RectTransform>();

        private RectTransform ResolveGuidedRackTile()
        {
            PlayerState local = _host.Players.GetById(_host.Players.LocalPlayerId);
            int index = FindGuidedRackIndex(local?.Rack, _host.GuidedRackTileId);
            return index >= 0 && index < _rackButtons.Count && _rackButtons[index].gameObject.activeInHierarchy
                ? _rackButtons[index].GetComponent<RectTransform>() : null;
        }

        private RectTransform ResolveExchangeTarget()
        {
            if (!_exchangeMode)
                return ResolveCommandTarget(_exchangeButton);
            int index = NextExchangeIndex(_host.GuidedExchangeIndices, _exchangeSelection);
            if (index >= 0 && index < _rackButtons.Count)
                return _rackButtons[index].GetComponent<RectTransform>();
            return ResolveCommandTarget(_exchangeButton);
        }

        internal static int FindGuidedRackIndex(IReadOnlyList<byte> rack, byte? tileId)
        {
            if (rack == null || !tileId.HasValue) return -1;
            for (int i = 0; i < rack.Count; i++)
                if (rack[i] == tileId.Value) return i;
            return -1;
        }

        internal static int NextExchangeIndex(IReadOnlyList<int> required, IReadOnlyList<int> selected)
        {
            if (required == null) return -1;
            for (int i = 0; i < required.Count; i++)
                if (selected == null || !selected.Contains(required[i])) return required[i];
            return -1;
        }

        private bool IsGuidedRackChoice(int rackIndex)
        {
            if (!_host.GuidedRackTileId.HasValue)
                return true;

            PlayerState local = _host.Players.GetById(_host.Players.LocalPlayerId);
            return local != null
                && rackIndex >= 0
                && rackIndex < local.Rack.Count
                && local.Rack[rackIndex] == _host.GuidedRackTileId.Value;
        }

        private void ShowCorrection(string message)
        {
            _correction?.Invoke(message);
        }
    }
}
