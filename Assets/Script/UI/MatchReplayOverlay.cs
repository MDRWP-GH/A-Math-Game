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
        private readonly GameObject _root;
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

            _root = UiFactory.CreateRect("Replay Overlay", canvasTransform).gameObject;
            UiFactory.Stretch(_root.GetComponent<RectTransform>());

            var catcher = UiFactory.CreateImage("Catcher", _root.transform, new Color(0f, 0f, 0f, 0.55f));
            catcher.raycastTarget = true;
            UiFactory.Stretch(catcher.rectTransform);

            var panel = UiFactory.CreateImage("Panel", _root.transform, new Color(0.05f, 0.05f, 0.07f, 0.95f));
            panel.raycastTarget = true;
            UiFactory.SetCenteredRect(panel.rectTransform, Vector2.zero, new Vector2(860f, 680f));

            _title = _ui.CreateText("Title", panel.transform, string.Empty, 36, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_title.rectTransform, new Vector2(0f, 270f), new Vector2(760f, 60f));

            _body = _ui.CreateText("Body", panel.transform, string.Empty, 26, FontStyle.Normal, UiPalette.LightText, TextAnchor.UpperLeft);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetCenteredRect(_body.rectTransform, new Vector2(0f, 20f), new Vector2(760f, 380f));

            var prev = _ui.CreateButton(panel.transform, "Prev", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, ShowPrevious);
            UiFactory.SetCenteredRect(prev.GetComponent<RectTransform>(), new Vector2(-180f, -250f), new Vector2(220f, 60f));
            LocalizedText.Bind(prev.GetComponentInChildren<Text>(), "ui.history.prev");

            var next = _ui.CreateButton(panel.transform, "Next", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, ShowNext);
            UiFactory.SetCenteredRect(next.GetComponent<RectTransform>(), new Vector2(180f, -250f), new Vector2(220f, 60f));
            LocalizedText.Bind(next.GetComponentInChildren<Text>(), "ui.history.next");

            var close = _ui.CreateButton(panel.transform, "Close", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, Close);
            UiFactory.SetCenteredRect(close.GetComponent<RectTransform>(), new Vector2(0f, -330f), new Vector2(240f, 56f));
            LocalizedText.Bind(close.GetComponentInChildren<Text>(), "ui.history.close");

            _root.SetActive(false);
        }

        public void Open(string matchId)
        {
            if (!_historyStore.TryLoadReplay(matchId, out SaveFile file, out string error))
            {
                _title.text = _text.GetText("ui.history.replay_error");
                _body.text = error;
                _root.SetActive(true);
                return;
            }

            _file = file;
            _maxTurn = _file.Replay.Events?.Count ?? 0;
            _turnIndex = _maxTurn > 0 ? 1 : 0;
            _root.SetActive(true);
            Refresh();
        }

        public void Close() => _root.SetActive(false);

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
