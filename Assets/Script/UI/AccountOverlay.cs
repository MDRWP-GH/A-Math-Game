using System;
using AMath.Art;
using AMath.Core.Accounts;
using AMath.Settings;
using AMath.Core.Assistance;
using AMath.UI.Localization;
using AMath.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>Local account sign-in and registration overlay for the main menu.</summary>
    internal sealed class AccountOverlay
    {
        private readonly UiFactory _ui;
        private readonly ILocalizedTextProvider _text;
        private readonly GameObject _root;
        private readonly Text _status;
        private readonly Text _modeLabel;
        private readonly InputField _usernameField;
        private readonly InputField _passwordField;
        private readonly InputField _displayNameField;
        private readonly GameObject _displayNameRow;
        private readonly Button _toggleModeButton;
        private bool _registerMode;

        public event Action SignedIn;
        public event Action Closed;

        public bool IsOpen => _root.activeSelf;

        public AccountOverlay(UiFactory ui, Transform canvasTransform, ILocalizedTextProvider text)
        {
            _ui = ui;
            _text = text;

            _root = UiFactory.CreateRect("Account Overlay", canvasTransform).gameObject;
            UiFactory.Stretch(_root.GetComponent<RectTransform>());

            var catcher = UiFactory.CreateImage("Catcher", _root.transform, new Color(0f, 0f, 0f, 0.45f));
            catcher.raycastTarget = true;
            UiFactory.Stretch(catcher.rectTransform);

            var panel = UiFactory.CreateImage("Panel", _root.transform, new Color(0.05f, 0.05f, 0.07f, 0.92f));
            panel.raycastTarget = true;
            UiFactory.SetCenteredRect(panel.rectTransform, Vector2.zero, new Vector2(760f, 620f));

            var title = _ui.CreateText("Title", panel.transform, string.Empty, 52, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 240f), new Vector2(640f, 70f));
            LocalizedText.Bind(title, "ui.account.title");

            _modeLabel = _ui.CreateText("Mode", panel.transform, string.Empty, 28, FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_modeLabel.rectTransform, new Vector2(0f, 170f), new Vector2(640f, 40f));

            _usernameField = _ui.CreateInputField(panel.transform, "Username", string.Empty, text.GetText("ui.account.username"), 30);
            UiFactory.SetCenteredRect(_usernameField.GetComponent<RectTransform>(), new Vector2(0f, 90f), new Vector2(560f, 56f));

            _passwordField = _ui.CreateInputField(panel.transform, "Password", string.Empty, text.GetText("ui.account.password"), 30);
            _passwordField.contentType = InputField.ContentType.Password;
            UiFactory.SetCenteredRect(_passwordField.GetComponent<RectTransform>(), new Vector2(0f, 10f), new Vector2(560f, 56f));

            _displayNameRow = UiFactory.CreateRect("DisplayNameRow", panel.transform).gameObject;
            UiFactory.SetCenteredRect(_displayNameRow.GetComponent<RectTransform>(), new Vector2(0f, -70f), new Vector2(560f, 56f));
            _displayNameField = _ui.CreateInputField(_displayNameRow.transform, "DisplayName", string.Empty, text.GetText("ui.account.display_name"), 30);
            UiFactory.SetStretchRect(_displayNameField.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            _status = _ui.CreateText("Status", panel.transform, string.Empty, 22, FontStyle.Italic, UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_status.rectTransform, new Vector2(0f, -140f), new Vector2(640f, 40f));

            var submit = _ui.CreateButton(panel.transform, "Submit", string.Empty, UiPalette.Primary, UiPalette.PrimaryHighlight, Submit);
            UiFactory.SetCenteredRect(submit.GetComponent<RectTransform>(), new Vector2(-120f, -220f), new Vector2(280f, 64f));
            LocalizedText.Bind(submit.GetComponentInChildren<Text>(), "ui.account.submit");

            var toggle = _ui.CreateButton(panel.transform, "ToggleMode", string.Empty, UiPalette.Secondary, UiPalette.SecondaryHighlight, ToggleMode);
            UiFactory.SetCenteredRect(toggle.GetComponent<RectTransform>(), new Vector2(160f, -220f), new Vector2(280f, 64f));
            _toggleModeButton = toggle;

            var close = _ui.CreateButton(panel.transform, "Close", string.Empty, UiPalette.Quit, UiPalette.QuitHighlight, Close);
            UiFactory.SetCenteredRect(close.GetComponent<RectTransform>(), new Vector2(0f, -310f), new Vector2(240f, 56f));
            LocalizedText.Bind(close.GetComponentInChildren<Text>(), "ui.account.close");

            _root.SetActive(false);
            SetRegisterMode(false);
        }

        public void Open()
        {
            _status.text = string.Empty;
            _usernameField.text = UserAccountStore.SessionUsername ?? string.Empty;
            _passwordField.text = string.Empty;
            _displayNameField.text = GameSettings.PlayerName;
            _root.SetActive(true);
        }

        public void Close()
        {
            _root.SetActive(false);
            Closed?.Invoke();
        }

        private void ToggleMode()
        {
            SetRegisterMode(!_registerMode);
        }

        private void SetRegisterMode(bool registerMode)
        {
            _registerMode = registerMode;
            _displayNameRow.SetActive(registerMode);
            _modeLabel.text = _text.GetText(registerMode ? "ui.account.mode_register" : "ui.account.mode_login");
            _toggleModeButton.GetComponentInChildren<Text>().text = _text.GetText(
                registerMode ? "ui.account.switch_login" : "ui.account.switch_register");
        }

        private void Submit()
        {
            _status.text = string.Empty;
            string username = _usernameField.text;
            string password = _passwordField.text;

            if (_registerMode)
            {
                if (!UserAccountStore.Register(username, password, _displayNameField.text, out string error))
                {
                    _status.text = LocalizeError(error);
                    return;
                }
            }
            else if (!UserAccountStore.SignIn(username, password, out string error))
            {
                _status.text = LocalizeError(error);
                return;
            }

            ApplySessionToIdentity();
            SignedIn?.Invoke();
            Close();
        }

        private static void ApplySessionToIdentity()
        {
            if (!UserAccountStore.TryGetSessionAccount(out UserAccountRecord account))
                return;

            GameSettings.PlayerName = account.DisplayName;
            LocalIdentity.DisplayName = account.DisplayName;
        }

        private string LocalizeError(string error) =>
            error switch
            {
                "Invalid username." => _text.GetText("ui.account.err_username"),
                "Password too short." => _text.GetText("ui.account.err_password"),
                "Username already exists." => _text.GetText("ui.account.err_exists"),
                "Account not found." => _text.GetText("ui.account.err_not_found"),
                "Incorrect password." => _text.GetText("ui.account.err_password_wrong"),
                _ => error
            };
    }
}
