using System;
using System.Collections.Generic;
using System.Text;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.History;
using AMath.Save;
using AMath.Utilities;
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

            var viewport = UiFactory.CreateImage("Viewport", panel.transform, new Color(1f, 1f, 1f, 0.04f));
            viewport.raycastTarget = true;
            UiFactory.SetCenteredRect(viewport.rectTransform, new Vector2(0f, -10f), new Vector2(780f, 500f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect("List", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UiFactory.AddVerticalLayout(content.gameObject, 8f, new RectOffset(8, 8, 8, 8));
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _listRoot = content;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

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
            foreach (MatchHistoryEntry entry in entries)
            {
                MatchHistoryEntry captured = entry;
                string label = BuildEntryLabel(captured);
                int lineCount = 1 + CountLines(label);
                float height = Mathf.Max(88f, 28f + lineCount * 24f);

                var button = _ui.CreateButton(_listRoot, "Entry", label, UiPalette.Secondary, UiPalette.SecondaryHighlight,
                    () => _replayOverlay.Open(captured.MatchId), 22);
                var text = button.GetComponentInChildren<Text>();
                text.alignment = TextAnchor.UpperLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.SetStretchRect(text.rectTransform, 18f, 10f, 18f, 10f);
                UiFactory.SetLayoutSize(button.gameObject, 0f, height, 1f);
            }
        }

        private string BuildEntryLabel(MatchHistoryEntry entry)
        {
            string formatLabel = entry.Format == MatchFormat.Team
                ? _text.GetText("ui.history.format_team")
                : _text.GetText("ui.history.format_individual");
            string when = FormatPlayedAt(entry);

            var builder = new StringBuilder();
            if (TryGetLocalResult(entry, out string playerName, out int score, out bool didWin))
            {
                builder.AppendLine(_text.GetText(didWin ? "ui.history.win" : "ui.history.lose"));
                builder.AppendLine(string.Format(
                    _text.GetText("ui.history.player_score"),
                    playerName,
                    score));
            }

            builder.Append(when);
            builder.Append("  |  ");
            builder.Append(formatLabel);
            builder.Append("  |  ");
            builder.Append(FormatDuration(entry.DurationSeconds));
            return builder.ToString().TrimEnd();
        }

        private static bool TryGetLocalResult(
            MatchHistoryEntry entry,
            out string playerName,
            out int score,
            out bool didWin)
        {
            if (entry.HasLocalPlayer)
            {
                playerName = entry.LocalPlayerName;
                score = entry.LocalPlayerScore;
                didWin = entry.DidWin;
                return true;
            }

            string localName = LocalIdentity.DisplayName;
            if (entry.Players != null && !string.IsNullOrEmpty(localName))
            {
                foreach (PlayerResult player in entry.Players)
                {
                    if (!string.Equals(player.DisplayName, localName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    playerName = player.DisplayName;
                    score = player.FinalScore;
                    didWin = entry.Format == MatchFormat.Team
                        ? player.TeamId >= 0 && entry.WinnerLabel == $"Team {player.TeamId + 1}"
                        : entry.WinnerLabel == player.DisplayName;
                    return true;
                }
            }

            playerName = null;
            score = 0;
            didWin = false;
            return false;
        }

        private static string FormatPlayedAt(MatchHistoryEntry entry)
        {
            long ticks = entry.StartedUtcTicks > 0 ? entry.StartedUtcTicks : entry.FinishedUtcTicks;
            if (ticks <= 0)
                return string.Empty;

            return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        }

        private string FormatDuration(int seconds)
        {
            int minutes = seconds / 60;
            int remain = seconds % 60;
            return minutes > 0
                ? string.Format(_text.GetText("ui.history.duration_min"), minutes, remain)
                : string.Format(_text.GetText("ui.history.duration_sec"), remain);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 1;

            int lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    lines++;
            }

            return lines;
        }
    }
}
