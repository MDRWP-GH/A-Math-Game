using System.Linq;
using System.Text;
using AMath.Core.Assistance;
using AMath.Replay;
using AMath.Save;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>Steps through an archived replay turn by turn.</summary>
    internal sealed class MatchReplayOverlay
    {
        private readonly UiFactory _ui;
        private readonly ILocalizedTextProvider _text;
        private readonly MatchHistoryStore _historyStore;
        private readonly OverlayShell _shell;
        private readonly Text _title;
        private readonly Text _body;
        private SaveFile _file;
        private int _turnIndex;
        private int _maxTurn;

        public MatchReplayOverlay(UiFactory ui, Transform canvasTransform, ILocalizedTextProvider text)
        {
            _ui = ui;
            _text = text;
            _historyStore = new MatchHistoryStore();

            _shell = UiFactory.CreateOverlayShell(
                canvasTransform,
                "Replay Overlay",
                includeGlassCard: true,
                cardSize: new Vector2(860f, 680f),
                glassColor: UiPalette.GlassStrong);
            var panel = _shell.Card.transform;

            _title = _ui.CreateOutlinedTitle(panel, "Title", string.Empty, 36);
            UiFactory.SetCenteredRect(_title.rectTransform, new Vector2(0f, 270f), new Vector2(760f, 60f));

            _body = _ui.CreateText("Body", panel, string.Empty, 26, FontStyle.Normal, UiPalette.LightText, TextAnchor.UpperLeft);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetCenteredRect(_body.rectTransform, new Vector2(0f, 20f), new Vector2(760f, 380f));

            var prev = _ui.CreateAccentButton(
                panel, "Prev", string.Empty,
                UiPalette.Secondary, UiPalette.SecondaryHighlight, ShowPrevious, 24);
            UiFactory.SetCenteredRect(prev.GetComponent<RectTransform>(), new Vector2(-180f, -250f), new Vector2(220f, 60f));
            LocalizedText.Bind(prev.GetComponentInChildren<Text>(), "ui.history.prev");

            var next = _ui.CreateAccentButton(
                panel, "Next", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, ShowNext, 24);
            UiFactory.SetCenteredRect(next.GetComponent<RectTransform>(), new Vector2(180f, -250f), new Vector2(220f, 60f));
            LocalizedText.Bind(next.GetComponentInChildren<Text>(), "ui.history.next");

            var close = _ui.CreateTextMenuButton(panel, "Close", string.Empty, 32, Close);
            UiFactory.SetCenteredRect(close.GetComponent<RectTransform>(), new Vector2(0f, -330f), new Vector2(240f, 56f));
            LocalizedText.Bind(close.GetComponentInChildren<Text>(), "ui.history.close");
        }

        public bool IsOpen => _shell.IsOpen;

        public void Open(string matchId)
        {
            if (!_historyStore.TryLoadReplay(matchId, out SaveFile file, out string error))
            {
                _title.text = _text.GetText("ui.history.replay_error");
                _body.text = error;
                _shell.Open();
                return;
            }

            _file = file;
            _maxTurn = _file.Replay.Events?.Count ?? 0;
            _turnIndex = _maxTurn > 0 ? 1 : 0;
            _shell.Open();
            Refresh();
        }

        public void Close() => _shell.Close();

        private void ShowPrevious()
        {
            if (_turnIndex > 0)
            {
                _turnIndex--;
                Refresh();
            }
        }

        private void ShowNext()
        {
            if (_turnIndex < _maxTurn)
            {
                _turnIndex++;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_file?.Replay == null)
                return;

            _title.text = string.Format(
                _text.GetText("ui.history.replay_title"),
                _turnIndex,
                _maxTurn,
                _file.RoomName ?? _text.GetText("ui.history.unknown_room"));

            if (_turnIndex == 0)
            {
                _body.text = _text.GetText("ui.history.replay_start");
                return;
            }

            if (!ReplayReconstructor.TryReconstruct(_file.Replay, _turnIndex, out var snapshot, out string error))
            {
                _body.text = error;
                return;
            }

            ReplayEvent evt = _file.Replay.Events[_turnIndex - 1];
            var builder = new StringBuilder();
            builder.AppendLine(string.Format(_text.GetText("ui.history.replay_turn"), evt.Turn, evt.PlayerId, evt.ScoreDelta));
            builder.AppendLine();
            builder.AppendLine(_text.GetText("ui.result.standings"));
            foreach (var player in snapshot.Players.OrderByDescending(p => p.Score))
            {
                int teamId = snapshot.Config?.Players?.Find(id => id.PlayerId == player.PlayerId)?.TeamId ?? -1;
                string teamSuffix = teamId >= 0 ? $" (T{teamId + 1})" : string.Empty;
                builder.AppendLine($"{player.DisplayName}{teamSuffix}: {player.Score}");
            }

            if (snapshot.Result != null)
                builder.AppendLine().AppendLine(_text.GetText("ui.history.replay_finished"));

            _body.text = builder.ToString();
        }
    }
}
