using System;
using AMath.Accounts;
using AMath.Art;
using AMath.UI.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>Full-screen login/register gate shown before any profile data is opened.</summary>
    internal sealed class AccountGateView
    {
        private enum Mode
        {
            Login,
            Register
        }

        private readonly UiFactory _ui;
        private readonly LocalAccountService _accounts;
        private readonly GameObject _root;
        private readonly MenuEntranceAnimator _entrance;
        private readonly MenuEntranceAnimator _modeEntrance;
        private readonly GameObject _confirmation;
        private readonly Text _heading;
        private readonly Text _message;
        private readonly Text _primaryLabel;
        private readonly Text _confirmationText;
        private readonly InputField _username;
        private readonly InputField _password;
        private readonly Button _loginTab;
        private readonly Button _registerTab;
        private readonly Button _primary;
        private readonly Button _confirmRegister;
        private readonly Button _cancelRegister;
        private Mode _mode;

        public AccountGateView(UiFactory ui, Transform owner, LocalAccountService accounts)
        {
            _ui = ui;
            _accounts = accounts;

            Canvas canvas = ui.CreateCanvas(owner, "Account Gate Canvas", 500);
            _root = canvas.gameObject;
            UiFactory.CreateFullScreenBackground(canvas.transform, "Main Menu Backgrounds", UiPalette.Background);

            Image shade = UiFactory.CreateImage("Shade", canvas.transform, new Color(0.01f, 0.03f, 0.04f, 0.36f));
            UiFactory.Stretch(shade.rectTransform);

            Text title = ui.CreateOutlinedTitle(canvas.transform, "Game Title", "A-MATH", 72);
            title.font = GameFonts.JainiPurva;
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(900f, 100f), new Vector2(0f, -52f));

            Image panel = UiFactory.CreateGlassPanel(canvas.transform, "Account Panel", UiPalette.GlassStrong);
            UiFactory.SetCenteredRect(panel.rectTransform, new Vector2(0f, -28f), new Vector2(720f, 650f));
            _entrance = _root.AddComponent<MenuEntranceAnimator>();
            _entrance.SetTargets(title, panel);
            _entrance.Configure(0.28f, 0.05f, 0f);

            _heading = ui.CreateText("Heading", panel.transform, string.Empty, 45, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_heading.rectTransform, new Vector2(0f, 255f), new Vector2(620f, 58f));

            Text intro = ui.CreateText("Intro", panel.transform, Text("ui.account.subtitle"), 25, FontStyle.Normal,
                UiPalette.MutedText, TextAnchor.MiddleCenter);
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;
            intro.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.SetCenteredRect(intro.rectTransform, new Vector2(0f, 205f), new Vector2(610f, 54f));

            _loginTab = ui.CreateAccentButton(panel.transform, "Login Tab", Text("ui.account.login"),
                UiPalette.Secondary, UiPalette.SecondaryHighlight, () => SwitchMode(Mode.Login), 28);
            UiFactory.SetCenteredRect(_loginTab.GetComponent<RectTransform>(), new Vector2(-156f, 145f), new Vector2(292f, 54f));

            _registerTab = ui.CreateAccentButton(panel.transform, "Register Tab", Text("ui.account.register"),
                UiPalette.GlassRow, UiPalette.SecondaryHighlight, () => SwitchMode(Mode.Register), 28);
            UiFactory.SetCenteredRect(_registerTab.GetComponent<RectTransform>(), new Vector2(156f, 145f), new Vector2(292f, 54f));

            CreateFieldLabel(panel.transform, "Username Label", Text("ui.account.username"), 82f);
            _username = ui.CreateInputField(panel.transform, "Username Input", string.Empty,
                Text("ui.account.username_hint"), 28);
            _username.characterLimit = 24;
            UiFactory.SetCenteredRect(_username.GetComponent<RectTransform>(), new Vector2(0f, 35f), new Vector2(600f, 62f));

            CreateFieldLabel(panel.transform, "Password Label", Text("ui.account.password"), -24f);
            _password = ui.CreateInputField(panel.transform, "Password Input", string.Empty,
                Text("ui.account.password_hint"), 28);
            _password.characterLimit = 64;
            _password.contentType = InputField.ContentType.Password;
            _password.ForceLabelUpdate();
            UiFactory.SetCenteredRect(_password.GetComponent<RectTransform>(), new Vector2(0f, -71f), new Vector2(600f, 62f));

            _message = ui.CreateText("Message", panel.transform, string.Empty, 18, FontStyle.Normal,
                UiPalette.TimerWarning, TextAnchor.MiddleCenter);
            _message.horizontalOverflow = HorizontalWrapMode.Wrap;
            _message.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.SetCenteredRect(_message.rectTransform, new Vector2(0f, -125f), new Vector2(600f, 46f));

            _primary = ui.CreateAccentButton(panel.transform, "Primary Action", string.Empty,
                UiPalette.Success, UiPalette.SuccessHighlight, Submit, 33);
            _primaryLabel = _primary.GetComponentInChildren<Text>();
            UiFactory.SetCenteredRect(_primary.GetComponent<RectTransform>(), new Vector2(0f, -183f), new Vector2(600f, 68f));

            Text saveHint = ui.CreateText("Save Hint", panel.transform, Text("ui.account.save_hint"), 22,
                FontStyle.Normal, UiPalette.MutedText, TextAnchor.MiddleCenter);
            saveHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            saveHint.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.SetCenteredRect(saveHint.rectTransform, new Vector2(0f, -263f), new Vector2(600f, 72f));

            Text version = ui.CreateText("Game Version", canvas.transform,
                string.Format(Text("ui.account.version"), Application.version), 22, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.78f), TextAnchor.LowerRight);
            UiFactory.SetAnchoredRect(version.rectTransform, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(260f, 40f), new Vector2(-24f, 18f));

            _confirmation = BuildConfirmation(canvas.transform, out _confirmationText, out _confirmRegister, out _cancelRegister);
            _modeEntrance = _root.AddComponent<MenuEntranceAnimator>();
            _modeEntrance.SetTargets(_heading, _primary);
            _modeEntrance.Configure(0.2f, 0.02f, 0f);
            ConfigureNavigation();
            SetMode(Mode.Login);
            _root.SetActive(false);
        }

        public event Action Authenticated;

        public bool IsOpen => _root.activeSelf;

        internal InputField UsernameField => _username;
        internal InputField PasswordField => _password;
        internal GameObject Confirmation => _confirmation;
        internal Button RegisterTab => _registerTab;
        internal Button PrimaryButton => _primary;

        public void Open(AccountError initialError = AccountError.None)
        {
            _root.SetActive(true);
            _entrance.Play();
            _confirmation.SetActive(false);
            SetError(initialError);
            UiFactory.SelectFirstInteractable(_username, _password, _primary);
        }

        public void Close()
        {
            _entrance.Skip();
            _modeEntrance.Skip();
            _confirmation.SetActive(false);
            _root.SetActive(false);
        }

        public void ReplayEntrance()
        {
            if (_root.activeInHierarchy)
                _entrance.Play();
        }

        public void HandleCancel()
        {
            if (_confirmation.activeSelf)
                CancelRegistration();
        }

        private void CreateFieldLabel(Transform parent, string name, string value, float y)
        {
            Text label = _ui.CreateText(name, parent, value, 25, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleLeft);
            UiFactory.SetCenteredRect(label.rectTransform, new Vector2(0f, y), new Vector2(600f, 38f));
        }

        private void SetMode(Mode mode)
        {
            _mode = mode;
            _message.text = string.Empty;
            bool registering = mode == Mode.Register;
            _heading.text = Text(registering ? "ui.account.register_title" : "ui.account.login_title");
            _primaryLabel.text = Text(registering ? "ui.account.register" : "ui.account.login");
            SetButtonColor(_loginTab, registering ? UiPalette.GlassRow : UiPalette.Secondary);
            SetButtonColor(_registerTab, registering ? UiPalette.Secondary : UiPalette.GlassRow);
            UiFactory.SelectFirstInteractable(_username);
        }

        private void SwitchMode(Mode mode)
        {
            if (_mode == mode)
                return;
            SetMode(mode);
            _modeEntrance.Play();
        }

        private static void SetButtonColor(Button button, Color normal)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            button.colors = colors;
        }

        private void Submit()
        {
            if (_mode == Mode.Login)
            {
                if (_accounts.TryLogin(_username.text, _password.text, out AccountError error))
                {
                    _password.text = string.Empty;
                    Authenticated?.Invoke();
                }
                else
                {
                    SetError(error);
                    UiFactory.SelectFirstInteractable(_password, _username);
                }
                return;
            }

            if (!_accounts.ValidateRegistrationInput(_username.text, _password.text, out AccountError validationError))
            {
                SetError(validationError);
                return;
            }

            _confirmationText.text = string.Format(Text("ui.account.confirm_body"), _username.text.Trim());
            _confirmation.SetActive(true);
            UiFactory.SelectFirstInteractable(_confirmRegister, _cancelRegister);
        }

        private void ConfirmRegistration()
        {
            _confirmation.SetActive(false);
            if (_accounts.TryRegister(_username.text, _password.text, out AccountError error))
            {
                _password.text = string.Empty;
                Authenticated?.Invoke();
                return;
            }

            SetError(error);
            UiFactory.SelectFirstInteractable(_username, _password);
        }

        private void CancelRegistration()
        {
            _confirmation.SetActive(false);
            UiFactory.SelectFirstInteractable(_primary);
        }

        private GameObject BuildConfirmation(Transform parent, out Text body, out Button confirm, out Button cancel)
        {
            OverlayShell shell = UiFactory.CreateOverlayShell(parent, "Register Confirmation", true,
                new Vector2(640f, 330f), UiPalette.GlassStrong);
            GameObject root = shell.Root;
            Transform panel = shell.Card.transform;

            Text title = _ui.CreateOutlinedTitle(panel, "Title", Text("ui.account.confirm_title"), 39);
            UiFactory.SetCenteredRect(title.rectTransform, new Vector2(0f, 105f), new Vector2(560f, 54f));

            body = _ui.CreateText("Body", panel, string.Empty, 27, FontStyle.Normal,
                UiPalette.LightText, TextAnchor.MiddleCenter);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.SetCenteredRect(body.rectTransform, new Vector2(0f, 30f), new Vector2(540f, 82f));

            confirm = _ui.CreateAccentButton(panel, "Confirm", Text("ui.account.confirm"),
                UiPalette.Success, UiPalette.SuccessHighlight, ConfirmRegistration, 28);
            UiFactory.SetCenteredRect(confirm.GetComponent<RectTransform>(), new Vector2(-145f, -92f), new Vector2(260f, 62f));

            cancel = _ui.CreateAccentButton(panel, "Cancel", Text("ui.account.cancel"),
                UiPalette.Quit, UiPalette.QuitHighlight, CancelRegistration, 28);
            UiFactory.SetCenteredRect(cancel.GetComponent<RectTransform>(), new Vector2(145f, -92f), new Vector2(260f, 62f));

            UiFactory.SetVerticalNavigation(confirm, cancel, cancel);
            UiFactory.SetVerticalNavigation(cancel, confirm, confirm);
            return root;
        }

        private void ConfigureNavigation()
        {
            UiFactory.SetVerticalNavigation(_loginTab, _primary, _username);
            UiFactory.SetVerticalNavigation(_registerTab, _primary, _username);
            UiFactory.SetVerticalNavigation(_username, _loginTab, _password);
            UiFactory.SetVerticalNavigation(_password, _username, _primary);
            UiFactory.SetVerticalNavigation(_primary, _password, _loginTab);

            Navigation loginNavigation = _loginTab.navigation;
            loginNavigation.selectOnRight = _registerTab;
            _loginTab.navigation = loginNavigation;
            Navigation registerNavigation = _registerTab.navigation;
            registerNavigation.selectOnLeft = _loginTab;
            _registerTab.navigation = registerNavigation;
        }

        private void SetError(AccountError error)
        {
            _message.text = error == AccountError.None ? string.Empty : Text(ErrorKey(error));
        }

        private static string ErrorKey(AccountError error)
        {
            return error switch
            {
                AccountError.UsernameRequired => "ui.account.err_username_required",
                AccountError.UsernameTooLong => "ui.account.err_username_long",
                AccountError.PasswordRequired => "ui.account.err_password_required",
                AccountError.PasswordTooShort => "ui.account.err_password_short",
                AccountError.UsernameTaken => "ui.account.err_username_taken",
                AccountError.InvalidCredentials => "ui.account.err_invalid",
                AccountError.StorageUnavailable => "ui.account.err_storage",
                AccountError.CorruptData => "ui.account.err_corrupt",
                AccountError.AutoLoginExpired => "ui.account.err_session",
                AccountError.AccountConflict => "ui.account.err_conflict",
                AccountError.MigrationIncomplete => "ui.account.err_migration",
                _ => "ui.account.err_invalid"
            };
        }

        private static string Text(string key) => UiLocalizationProvider.Shared.GetText(key);
    }
}
