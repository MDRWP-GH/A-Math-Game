using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.History;
using AMath.Save;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>Browses archived match results on the main menu.</summary>
    internal sealed class MatchHistoryOverlay
    {
        private readonly UiFactory _ui;
        private readonly ILocalizedTextProvider _text;
        private readonly MatchHistoryStore _historyStore;
        private readonly GameObject _root;
        private readonly Transform _listRoot;
        private readonly Text _status;
        private readonly MatchReplayOverlay _replayOverlay;

        public event Action Closed;

        public bool IsOpen => _root.activeSelf;

        public MatchHistoryOverlay(UiFactory ui, Transform canvasTransform, ILocalizedTextProvider text)
        {
            _ui = ui;
            _text = text;
            _historyStore = new MatchHistoryStore();
            _replayOverlay = new MatchReplayOverlay(ui, canvasTransform, text);

            _root = UiFactory.CreateRect("History Overlay", canvasTransform).gameObject;
            UiFactory.Stretch(_root.GetComponent<RectTransform>());

            var catcher = UiFactory.CreateImage("Catcher", _root.transform, new Color(0f, 0f, 0f, 0.45f));
            catcher.raycastTarget = true;
            UiFactory.Stretch(catcher.rectTransform);

            var panel = UiFactory.CreateImage("Panel", _root.transform, new Color(0.05f, 0.05f, 0.07f, 0.92f));
            panel.raycastTarget = true;
            UiFactory.SetCenteredRect(panel.rectTransform, Vector2.zero, new Vector2(900f, 720f));

            var title = _ui.CreateText("Title", panel.transform, string.Empty, 52, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 300f), new Vector2(760f, 70f));
            LocalizedText.Bind(title, "ui.history.title");

            _listRoot = UiFactory.CreateRect("List", panel.transform).transform;
            UiFactory.SetCenteredRect((RectTransform)_listRoot, new Vector2(0f, -20f), new Vector2(760f, 480f));

            _status = _ui.CreateText("Status", panel.transform, string.Empty, 24, FontStyle.Italic, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_status.rectTransform, new Vector2(0f, -280f), new Vector2(760f, 40f));

            var close = _ui.CreateButton(panel.transform, "Close", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, Close);
            UiFactory.SetCenteredRect(close.GetComponent<RectTransform>(), new Vector2(0f, -340f), new Vector2(280f, 60f));
            LocalizedText.Bind(close.GetComponentInChildren<Text>(), "ui.history.close");

            _root.SetActive(false);
        }

        public void Open()
        {
            _root.SetActive(true);
            RefreshList();
        }

        public void Close()
        {
            _root.SetActive(false);
            Closed?.Invoke();
        }

        private void RefreshList()
        {
            for (int i = _listRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_listRoot.GetChild(i).gameObject);

            IReadOnlyList<MatchHistoryEntry> entries = _historyStore.ListEntries();
            if (entries.Count == 0)
            {
                _status.text = _text.GetText("ui.history.empty");
                return;
            }

            _status.text = string.Empty;
            float y = 210f;
            foreach (MatchHistoryEntry entry in entries)
            {
                MatchHistoryEntry captured = entry;
                string formatLabel = captured.Format == MatchFormat.Team
                    ? _text.GetText("ui.history.format_team")
                    : _text.GetText("ui.history.format_individual");
                string when = new DateTime(captured.FinishedUtcTicks, DateTimeKind.Utc).ToLocalTime()
                    .ToString("g");
                string label = $"{when}  |  {formatLabel}  |  {FormatDuration(captured.DurationSeconds)}\n" +
                               $"{captured.WinnerLabel} ({captured.WinnerScore})  —  {captured.RoomName}";

                var button = _ui.CreateButton(_listRoot, "Entry", label, UiPalette.Secondary, UiPalette.SecondaryHighlight,
                    () => _replayOverlay.Open(captured.MatchId));
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                UiFactory.SetCenteredRect(button.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(720f, 72f));
                y -= 80f;
            }
        }

        private string FormatDuration(int seconds)
        {
            int minutes = seconds / 60;
            int remain = seconds % 60;
            return minutes > 0
                ? string.Format(_text.GetText("ui.history.duration_min"), minutes, remain)
                : string.Format(_text.GetText("ui.history.duration_sec"), remain);
        }
    }
}
