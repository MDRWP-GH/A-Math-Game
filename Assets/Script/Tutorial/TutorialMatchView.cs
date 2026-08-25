using System.Collections.Generic;
using AMath.Art;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Gameplay.Board;
using AMath.Gameplay.Interaction;
using AMath.Gameplay.Players;
using AMath.Tutorial.Scripted;
using UnityEngine;
using UnityEngine.UI;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.UI.Tutorial
{
    /// <summary>
    /// Code-driven board, rack and confirm/clear controls for the scripted
    /// tutorial match. Pass, exchange and AI are intentionally omitted.
    /// </summary>
    internal sealed class TutorialMatchView
    {
        private const float RackPitch = 72f;
        private const float CellSize = 34f;

        private readonly UiFactory _ui;
        private readonly ILocalizedTextProvider _text;
        private readonly TutorialMatchHost _host;
        private readonly MatchBoardView _boardView;
        private readonly Transform _rackRoot;
        private readonly Text _statusText;
        private readonly Text _scoreText;
        private readonly Text _errorText;
        private readonly Button _confirmButton;
        private readonly Button _clearButton;
        private readonly List<Button> _rackButtons = new(GameRules.RackSize);
        private readonly List<Text> _rackLabels = new(GameRules.RackSize);

        public TutorialMatchView(
            UiFactory ui,
            ILocalizedTextProvider text,
            TutorialMatchHost host,
            Transform canvas)
        {
            _ui = ui;
            _text = text;
            _host = host;

            _statusText = _ui.CreateText(
                "Match Status", canvas, string.Empty, 22, FontStyle.Bold,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchoredRect(
                _statusText.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(720f, 36f), new Vector2(0f, -88f));

            _scoreText = _ui.CreateText(
                "Scores", canvas, string.Empty, 18, FontStyle.Normal,
                UiPalette.MutedText, TextAnchor.UpperLeft);
            _scoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetAnchoredRect(
                _scoreText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(260f, 80f), new Vector2(24f, -80f));

            RectTransform boardRoot = UiFactory.CreateRect("Board", canvas);
            UiFactory.SetCenteredRect(boardRoot, new Vector2(0f, 55f), new Vector2(530f, 530f));
            _boardView = new MatchBoardView(_ui, boardRoot, OnCellClicked, CellSize);

            var boardHighlight = UiFactory.CreateImage(
                "Demo Board Highlight",
                boardRoot,
                new Color(0.98f, 0.84f, 0.18f, 0.18f));
            UiFactory.Stretch(boardHighlight.rectTransform);
            BoardHighlight = boardHighlight.gameObject;
            BoardHighlight.SetActive(false);

            _rackRoot = UiFactory.CreateRect("Rack", canvas).transform;
            UiFactory.SetAnchoredRect(
                (RectTransform)_rackRoot,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(720f, 78f), new Vector2(0f, 210f));

            _confirmButton = _ui.CreateButton(
                canvas, "Confirm", string.Empty,
                UiPalette.Primary, UiPalette.PrimaryHighlight,
                () => Confirm(), 20);
            UiFactory.SetAnchoredRect(
                _confirmButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(180f, 48f), new Vector2(-100f, 155f));
            _confirmButton.GetComponentInChildren<Text>().text = _text.GetText("tutorial.ui.confirm");

            _clearButton = _ui.CreateButton(
                canvas, "Clear", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight,
                () => ClearDraft(), 20);
            UiFactory.SetAnchoredRect(
                _clearButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(180f, 48f), new Vector2(100f, 155f));
            _clearButton.GetComponentInChildren<Text>().text = _text.GetText("tutorial.ui.clear");

            _errorText = _ui.CreateText(
                "Match Error", canvas, string.Empty, 18, FontStyle.Italic,
                UiPalette.HintText, TextAnchor.MiddleCenter);
            _errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetAnchoredRect(
                _errorText.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(720f, 36f), new Vector2(0f, 118f));

            _host.Changed += Refresh;
            Refresh();
        }

        /// <summary>Overlay used by the authored board-highlight step.</summary>
        public GameObject BoardHighlight { get; }

        /// <summary>Repaints board, rack and turn status.</summary>
        public void Refresh()
        {
            bool myTurn = _host.IsPlayerInputEnabled;
            _statusText.text = myTurn
                ? _text.GetText("tutorial.ui.your_turn")
                : _text.GetText("tutorial.ui.wait_script");

            var scores = new System.Text.StringBuilder();
            foreach (PlayerState player in _host.Players.Players)
            {
                string you = player.PlayerId == _host.Players.LocalPlayerId
                    ? $" ({_text.GetText("tutorial.ui.you")})"
                    : string.Empty;
                string bot = player.IsAi ? $" ({_text.GetText("tutorial.ui.bot")})" : string.Empty;
                string line = $"{player.DisplayName}{bot}{you}: {player.Score}";
                if (player.PlayerId == _host.Turns.CurrentPlayerId)
                    line = $"<color=#FDA733><b>▶ {line}</b></color>";
                scores.AppendLine(line);
            }

            _scoreText.text = scores.ToString();
            _errorText.text = _host.Readers.LastCommandRejectionReason ?? string.Empty;

            _boardView.SetGuideCells(_host.RemainingGuidePlacements);
            _boardView.Refresh(_host.Board.Grid, _host.Input);

            _confirmButton.interactable = myTurn;
            _clearButton.interactable = myTurn;
            RefreshRack(myTurn);
        }

        public void Dispose()
        {
            _host.Changed -= Refresh;
        }

        private void RefreshRack(bool myTurn)
        {
            PlayerState local = _host.Players.GetById(_host.Players.LocalPlayerId);
            int tileCount = local?.Rack.Count ?? 0;
            EnsureRackButtons(tileCount);

            HashSet<int> needed = NeededRackIndices(local);
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
                bool selected = _host.Input.SelectedRackIndex == i;
                bool guide = needed.Contains(i);
                button.gameObject.SetActive(true);
                button.interactable = myTurn;
                Color color = selected ? UiPalette.Primary : guide ? new Color(0.98f, 0.84f, 0.18f, 1f) : UiPalette.Secondary;
                ColorBlock colors = button.colors;
                colors.normalColor = color;
                colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
                button.colors = colors;
                _rackLabels[i].text = $"{SymbolOf(tileId)}\n{PointsOf(tileId)}";
                UiFactory.SetCenteredRect(
                    button.GetComponent<RectTransform>(),
                    new Vector2(start + i * RackPitch, 0f),
                    new Vector2(64f, 70f));
            }
        }

        private HashSet<int> NeededRackIndices(PlayerState local)
        {
            var needed = new HashSet<int>();
            if (local == null || !_host.IsPlayerInputEnabled)
                return needed;

            IReadOnlyList<TilePlacement> remaining = _host.RemainingGuidePlacements;
            var used = new bool[local.Rack.Count];
            for (int p = 0; p < remaining.Count; p++)
            {
                byte tileId = remaining[p].TileId;
                for (int i = 0; i < local.Rack.Count; i++)
                {
                    if (used[i] || local.Rack[i] != tileId) continue;
                    used[i] = true;
                    needed.Add(i);
                    break;
                }
            }

            return needed;
        }

        private void EnsureRackButtons(int required)
        {
            while (_rackButtons.Count < required)
            {
                int index = _rackButtons.Count;
                var button = _ui.CreateButton(
                    _rackRoot, $"R{index}", string.Empty,
                    UiPalette.Secondary, UiPalette.PrimaryHighlight,
                    () => OnRackClicked(index), 18);
                Text label = button.GetComponentInChildren<Text>();
                if (label != null)
                    label.alignment = TextAnchor.MiddleCenter;
                _rackButtons.Add(button);
                _rackLabels.Add(label);
            }
        }

        private void OnRackClicked(int index)
        {
            if (!_host.IsPlayerInputEnabled) return;
            _host.Input.SelectFromRack(index);
        }

        private void OnCellClicked(int x, int y)
        {
            if (!_host.IsPlayerInputEnabled) return;

            TurnInputSession input = _host.Input;
            for (int i = 0; i < input.PendingPlacements.Count; i++)
            {
                if (input.PendingPlacements[i].X == x && input.PendingPlacements[i].Y == y)
                {
                    input.RemovePendingAt(x, y);
                    return;
                }
            }

            if (!input.TryPlaceOnCell(x, y, out string error) && !string.IsNullOrEmpty(error))
                _errorText.text = error;
        }

        private void Confirm()
        {
            if (!_host.Input.TryConfirmPlace(out string error) && !string.IsNullOrEmpty(error))
                _errorText.text = error;
        }

        private void ClearDraft() => _host.Input.ClearDraft();
    }
}
