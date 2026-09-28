using System.Reflection;
using AMath.Accounts;
using AMath.Art;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Gameplay.Board;
using AMath.Tutorial.Localization;
using AMath.UI;
using AMath.UI.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class UiPresentationTests
    {
        [Test]
        public void FramedButtons_UsePointFilteredPixelCorners_AndChoiceSurvivesHover()
        {
            var root = new GameObject("Pixel Button Test", typeof(RectTransform));
            var eventSystem = new GameObject("Event System", typeof(EventSystem));
            try
            {
                var ui = new UiFactory(GameFonts.Jersey25);
                Button choice = ui.CreateButton(root.transform, "Choice", "Team", UiPalette.Secondary,
                    UiPalette.SecondaryHighlight, () => { });
                Image baseImage = choice.GetComponent<Image>();
                Assert.AreEqual(Image.Type.Sliced, baseImage.type);
                Assert.AreEqual(FilterMode.Point, baseImage.sprite.texture.filterMode);
                Assert.Greater(baseImage.sprite.border.x, 0f);

                UiFactory.SetChoiceSelected(choice, true);
                Image selected = choice.transform.Find("Selected Fill").GetComponent<Image>();
                Assert.IsTrue(selected.gameObject.activeSelf);
                Assert.IsFalse(selected.raycastTarget);
                Assert.AreEqual(UiPalette.Primary, selected.color);
                choice.OnPointerEnter(new PointerEventData(eventSystem.GetComponent<EventSystem>()));
                Assert.IsTrue(selected.gameObject.activeSelf);
                UiFactory.SetChoiceSelected(choice, false);
                Assert.IsFalse(selected.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(eventSystem);
            }
        }

        [Test]
        public void InterruptedRackDrag_ClearsGlobalDragState()
        {
            var canvasObject = new GameObject("Drag Canvas", typeof(RectTransform), typeof(Canvas));
            var sourceObject = new GameObject("Rack Tile", typeof(RectTransform));
            var eventSystemObject = new GameObject("Event System", typeof(EventSystem));
            sourceObject.transform.SetParent(canvasObject.transform, false);
            try
            {
                var source = sourceObject.AddComponent<RackTileDragSource>();
                source.Configure(2, () => true, () => null, null, null, null);
                source.OnBeginDrag(new PointerEventData(eventSystemObject.GetComponent<EventSystem>()));
                Assert.IsTrue(TileDragState.IsDragging);
                Assert.AreEqual(2, TileDragState.RackIndex);

                source.enabled = false;
                // EditMode does not dispatch MonoBehaviour lifecycle callbacks
                // for non-ExecuteAlways components; invoke the runtime hook.
                InvokePrivate(source, "OnDisable");
                Assert.IsFalse(TileDragState.IsDragging);
                Assert.AreEqual(-1, TileDragState.RackIndex);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(eventSystemObject);
                TileDragState.End();
            }
        }
        [Test]
        public void MainMenu_PlacesSettingsBelowHowToPlay_WithoutTestButton()
        {
            var root = new GameObject("Main Menu Layout Test");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<MainMenuController>();
                SetPrivateField(controller, "_ui", new UiFactory(GameFonts.Jersey25));
                LogAssert.Expect(
                    LogType.Error,
                    $"A-Math: scene '{TutorialSceneBootstrap.SceneName}' is missing from Build Settings.");
                InvokePrivate(controller, "BuildMenu");

                Transform canvas = root.transform.Find("Canvas");
                Assert.NotNull(canvas);
                Transform help = FindDescendant(canvas, "How To Play Button");
                Transform settings = FindDescendant(canvas, "Settings Button");
                Assert.NotNull(help);
                Assert.NotNull(settings);
                Assert.IsNull(FindDescendant(canvas, "Test Button"));
                Assert.NotNull(canvas.GetComponent<CanvasGroup>());
                Image logoutImage = FindDescendant(canvas, "Logout Button").GetComponent<Image>();
                Assert.AreEqual(Image.Type.Sliced, logoutImage.type);
                float helpY = help.GetComponent<RectTransform>().anchoredPosition.y;
                float settingsY = settings.GetComponent<RectTransform>().anchoredPosition.y;
                Assert.That(settingsY, Is.LessThan(helpY));
                Assert.That(helpY - settingsY, Is.EqualTo(70f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MenuEntrance_SkipAndDisableRestoreVisibilityAndScale()
        {
            var root = new GameObject("Entrance Test");
            var button = new GameObject("Button", typeof(RectTransform), typeof(CanvasGroup));
            button.transform.SetParent(root.transform, false);
            button.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            try
            {
                var entrance = root.AddComponent<MenuEntranceAnimator>();
                entrance.SetTargets(button.transform);
                entrance.Play();
                CanvasGroup group = button.GetComponent<CanvasGroup>();
                Assert.AreEqual(0f, group.alpha, 0.001f);
                Assert.IsFalse(group.blocksRaycasts);
                Assert.That(button.transform.localScale.x, Is.LessThan(1.2f));

                entrance.Skip();
                Assert.AreEqual(1f, group.alpha, 0.001f);
                Assert.IsTrue(group.blocksRaycasts);
                Assert.AreEqual(1.2f, button.transform.localScale.x, 0.001f);

                entrance.Play();
                root.SetActive(false);
                // EditMode does not dispatch MonoBehaviour disable callbacks reliably.
                InvokePrivate(entrance, "OnDisable");
                Assert.AreEqual(1f, group.alpha, 0.001f);
                Assert.IsTrue(group.blocksRaycasts);
                Assert.AreEqual(1.2f, button.transform.localScale.x, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OverlayFade_InterruptedCloseDoesNotCaptureInputOrLeaveCardScaled()
        {
            var root = new GameObject("Overlay Test", typeof(RectTransform));
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                OverlayFade fade = OverlayFade.Ensure(root);
                fade.FadeIn();
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                Assert.IsTrue(group.blocksRaycasts);
                // EditMode coroutines can complete in one tick; inspect the motion
                // mapping directly instead of depending on frame timing.
                InvokePrivate(fade, "SetContentProgress", 0f);
                Assert.That(panel.transform.localScale.x, Is.LessThan(1f));

                fade.FadeOut();
                Assert.IsFalse(group.blocksRaycasts, "A closing overlay must stop swallowing clicks immediately.");
                fade.FadeIn();
                Assert.IsTrue(group.blocksRaycasts);
                fade.HideInstant();
                Assert.IsFalse(root.activeSelf);
                Assert.IsFalse(group.blocksRaycasts);
                Assert.AreEqual(Vector3.one, panel.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AccountGate_OpenAndRegisterTabUseEntranceAnimation()
        {
            var root = new GameObject("Account Gate Test");
            try
            {
                var gate = new AccountGateView(new UiFactory(GameFonts.Jersey25), root.transform,
                    new LocalAccountService());
                gate.Open();
                Transform canvas = root.transform.Find("Account Gate Canvas");
                Assert.NotNull(canvas);
                CanvasGroup panel = canvas.Find("Account Panel").GetComponent<CanvasGroup>();
                Assert.AreEqual(0f, panel.alpha, 0.001f);
                gate.ReplayEntrance();
                Assert.AreEqual(0f, panel.alpha, 0.001f);
                gate.Close();
                Assert.AreEqual(1f, panel.alpha, 0.001f);
                Assert.IsFalse(canvas.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MultiplayerScreens_UseSeparatedCardsAndVisiblePrimaryActions()
        {
            var root = new GameObject("Multiplayer Layout Test");
            try
            {
                var controller = root.AddComponent<PlaySessionController>();
                var ui = new UiFactory(GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Test Canvas", 0);
                SetPrivateField(controller, "_ui", ui);
                SetPrivateField(controller, "_text", new EchoTextProvider());
                SetPrivateField(controller, "_canvas", canvas);

                InvokePrivate(controller, "BuildStartChoice");
                InvokePrivate(controller, "BuildHostSetup");
                InvokePrivate(controller, "BuildBrowser");
                InvokePrivate(controller, "BuildLobby");

                Transform startPanel = canvas.transform.Find("Start Choice/Panel");
                Assert.AreEqual(0f, startPanel.GetComponent<Image>().color.a);
                RectTransform hostCard = startPanel.Find("Host Card").GetComponent<RectTransform>();
                RectTransform joinCard = startPanel.Find("Join Card").GetComponent<RectTransform>();
                Assert.That(hostCard.GetComponent<Image>().color.a, Is.GreaterThan(0f));
                Assert.That(joinCard.GetComponent<Image>().color.a, Is.GreaterThan(0f));
                AssertSeparatedHorizontally(hostCard, joinCard);
                Assert.NotNull(hostCard.Find("Host").GetComponent<Button>());
                Assert.NotNull(joinCard.Find("Join").GetComponent<Button>());

                Transform lobbyPanel = canvas.transform.Find("Lobby/Panel");
                Assert.AreEqual(0f, lobbyPanel.GetComponent<Image>().color.a);
                RectTransform membersCard = lobbyPanel.Find("Members Card").GetComponent<RectTransform>();
                RectTransform setupCard = lobbyPanel.Find("Setup Card").GetComponent<RectTransform>();
                AssertSeparatedHorizontally(membersCard, setupCard);
                Assert.NotNull(setupCard.Find("StartMatch").GetComponent<Button>());
                Assert.NotNull(canvas.transform.Find("Host Setup/Panel/Create Room").GetComponent<Button>());
                Assert.NotNull(canvas.transform.Find("Join/Panel/Manual Code/JoinCodeBtn").GetComponent<Button>());
                Assert.AreEqual(0f, canvas.transform.Find("Join/Panel").GetComponent<Image>().color.a);
                Assert.NotNull(canvas.transform.Find("Start Choice").GetComponent<MenuEntranceAnimator>());
                Assert.NotNull(canvas.transform.Find("Host Setup").GetComponent<MenuEntranceAnimator>());
                Assert.NotNull(canvas.transform.Find("Join").GetComponent<MenuEntranceAnimator>());
                Assert.NotNull(canvas.transform.Find("Lobby").GetComponent<MenuEntranceAnimator>());
                InputField hostName = canvas.transform.Find("Host Setup/Panel/PlayerName").GetComponent<InputField>();
                InputField joinName = canvas.transform.Find("Join/Panel/Manual Code/PlayerName").GetComponent<InputField>();
                Assert.That(hostName.characterLimit, Is.GreaterThan(24),
                    "The field must retain overlong input long enough to explain why it is invalid.");
                Assert.That(joinName.characterLimit, Is.GreaterThan(24));

                Assert.That(startPanel.GetComponent<RectTransform>().sizeDelta.x, Is.LessThanOrEqualTo(1920f));
                Assert.That(lobbyPanel.GetComponent<RectTransform>().sizeDelta.y, Is.LessThanOrEqualTo(1080f));
                foreach (Vector2 resolution in new[] { new Vector2(1920f, 1080f), new Vector2(1280f, 720f) })
                {
                    AssertFitsScreen(startPanel.GetComponent<RectTransform>(), resolution);
                    AssertFitsScreen(canvas.transform.Find("Host Setup/Panel").GetComponent<RectTransform>(), resolution);
                    AssertFitsScreen(canvas.transform.Find("Join/Panel").GetComponent<RectTransform>(), resolution);
                    AssertFitsScreen(lobbyPanel.GetComponent<RectTransform>(), resolution);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TutorialChapterPicker_UsesForestShellAndThreeSeparatedCards()
        {
            var root = new GameObject("Tutorial Chapter Layout Test");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<TutorialSceneController>();
                var ui = new UiFactory(GameFonts.Jersey25);
                var textProvider = new TutorialLocalizationProvider();
                Canvas canvas = ui.CreateCanvas(root.transform, "Test Canvas", 0);
                SetPrivateField(controller, "_ui", ui);
                SetPrivateField(controller, "_textProvider", textProvider);
                SetPrivateField(controller, "_canvas", canvas);

                InvokePrivate(controller, "BuildChapterPicker");
                InvokePrivate(controller, "RefreshChapterPicker");

                Transform picker = canvas.transform.Find("Chapter Picker");
                Assert.NotNull(picker);
                Assert.NotNull(picker.Find("Background").GetComponent<Image>());
                Assert.NotNull(picker.Find("Title").GetComponent<Text>());
                Assert.NotNull(picker.Find("Subtitle").GetComponent<Text>());

                Transform panel = picker.Find("Panel");
                Assert.AreEqual(0f, panel.GetComponent<Image>().color.a);
                RectTransform intro = panel.Find("Intro Card").GetComponent<RectTransform>();
                RectTransform connect = panel.Find("Connect Card").GetComponent<RectTransform>();
                RectTransform premium = panel.Find("Premium Card").GetComponent<RectTransform>();
                AssertSeparatedHorizontally(intro, connect);
                AssertSeparatedHorizontally(connect, premium);

                Button introAction = intro.Find("Action").GetComponent<Button>();
                Button connectAction = connect.Find("Action").GetComponent<Button>();
                Button premiumAction = premium.Find("Action").GetComponent<Button>();
                Button back = panel.Find("Back").GetComponent<Button>();
                Assert.IsTrue(introAction.interactable);
                Assert.IsFalse(connectAction.interactable);
                Assert.IsFalse(premiumAction.interactable);
                Assert.NotNull(back);
                Assert.AreSame(back, introAction.navigation.selectOnDown);
                Assert.AreSame(introAction, back.navigation.selectOnUp);
                Assert.That(introAction.GetComponentInChildren<Text>().text, Is.Not.Empty);
                Assert.That(connectAction.GetComponentInChildren<Text>().text, Is.Not.Empty);
                Assert.That(premiumAction.GetComponentInChildren<Text>().text, Is.Not.Empty);
                Assert.That(panel.GetComponent<RectTransform>().sizeDelta.x, Is.LessThanOrEqualTo(1920f));
                Assert.That(panel.GetComponent<RectTransform>().sizeDelta.y, Is.LessThanOrEqualTo(1080f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TutorialCompletion_ShowsSavedOutcomesAndNextChapterAction()
        {
            var root = new GameObject("Tutorial Completion Test");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<TutorialSceneController>();
                var ui = new UiFactory(GameFonts.Jersey25);
                var textProvider = new TutorialLocalizationProvider();
                Canvas canvas = ui.CreateCanvas(root.transform, "Test Canvas", 0);
                SetPrivateField(controller, "_ui", ui);
                SetPrivateField(controller, "_textProvider", textProvider);
                SetPrivateField(controller, "_canvas", canvas);

                InvokePrivate(controller, "BuildOverlays");
                LogAssert.Expect(
                    LogType.Error,
                    "Coroutine couldn't be started because the the game object 'Tutorial Completion' is inactive!");
                InvokePrivate(controller, "ShowCompletionSummary", "intro");

                Transform completion = canvas.transform.Find("Tutorial Completion");
                Assert.NotNull(completion);
                Assert.IsTrue(completion.gameObject.activeSelf);
                Assert.AreEqual("CompletionSummary", GetPrivateField(controller, "_screenState").ToString());

                Text saved = FindDescendant(completion, "Saved").GetComponent<Text>();
                Text outcomes = FindDescendant(completion, "Outcomes").GetComponent<Text>();
                Button primary = FindDescendant(completion, "Primary").GetComponent<Button>();
                Assert.That(saved.text, Is.Not.Empty);
                Assert.That(outcomes.text, Does.Contain("•"));
                Assert.That(
                    primary.GetComponentInChildren<Text>().text,
                    Is.EqualTo(textProvider.GetText("tutorial.ui.completion_next")));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchHud_BuildsTimerGaugeAndStartsWithCommandsCollapsed()
        {
            var root = new GameObject("Match HUD Test");
            try
            {
                var controller = root.AddComponent<PlaySessionController>();
                var ui = new UiFactory(GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Test Canvas", 0);
                SetPrivateField(controller, "_ui", ui);
                SetPrivateField(controller, "_text", new EchoTextProvider());
                SetPrivateField(controller, "_canvas", canvas);

                InvokePrivate(controller, "BuildMatch");

                Transform timer = canvas.transform.Find("Match/Turn Timer");
                Assert.NotNull(timer);
                RectTransform timerRect = timer.GetComponent<RectTransform>();
                Assert.AreEqual(0f, timerRect.anchorMin.x, 0.001f);
                Assert.AreEqual(0.5f, timerRect.anchorMax.y, 0.001f);
                Assert.NotNull(timer.Find("Value").GetComponent<Text>());
                Assert.NotNull(timer.Find("Track/Fill").GetComponent<Image>());

                RectTransform toggleRect = canvas.transform.Find("Match/Command Drawer Toggle")
                    .GetComponent<RectTransform>();
                Assert.AreEqual(MatchHudLayout.Standard.CommandToggle.Position.x, toggleRect.anchoredPosition.x, 0.001f);
                Assert.AreEqual(MatchHudLayout.Standard.CommandToggle.Position.y, toggleRect.anchoredPosition.y, 0.001f);

                Transform drawer = canvas.transform.Find("Match/Command Drawer");
                Assert.NotNull(drawer);
                Assert.NotNull(drawer.Find("Confirm").GetComponent<Button>());
                Assert.NotNull(drawer.Find("Clear").GetComponent<Button>());
                Assert.NotNull(drawer.Find("Pass").GetComponent<Button>());
                Assert.NotNull(drawer.Find("Exchange").GetComponent<Button>());

                MatchCommandDrawerAnimator animator = drawer.GetComponent<MatchCommandDrawerAnimator>();
                Assert.NotNull(animator);
                Assert.IsFalse(animator.IsExpanded);
                Assert.AreEqual(0f, animator.Group.alpha, 0.001f);
                Assert.IsFalse(animator.Group.blocksRaycasts);
                Assert.IsFalse(animator.Group.interactable);

                animator.SetExpandedInstant(true);
                RectTransform board = canvas.transform.Find("Match/Board Frame").GetComponent<RectTransform>();
                RectTransform rack = canvas.transform.Find("Match/Rack Shelf").GetComponent<RectTransform>();
                RectTransform preview = canvas.transform.Find("Match/Preview").GetComponent<RectTransform>();
                RectTransform declare = canvas.transform.Find("Match/Declare").GetComponent<RectTransform>();
                Assert.IsFalse(ReferenceRect(board).Overlaps(ReferenceRect(rack)));
                Assert.IsFalse(ReferenceRect(board).Overlaps(ReferenceRect(preview)));
                Assert.IsFalse(ReferenceRect(board).Overlaps(ReferenceRect(declare)));
                Assert.IsFalse(ReferenceRect(rack).Overlaps(ReferenceRect(drawer.GetComponent<RectTransform>())));
                Assert.IsFalse(ReferenceRect(rack).Overlaps(ReferenceRect(toggleRect)));
                animator.SetExpandedInstant(false);

                Button toggle = canvas.transform.Find("Match/Command Drawer Toggle").GetComponent<Button>();
                Assert.NotNull(toggle);
                Assert.AreEqual("ui.match.commands_open", toggle.GetComponentInChildren<Text>().text);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchTimer_FormatsCeilingSecondsAndClampsProgress()
        {
            MethodInfo format = typeof(PlaySessionController).GetMethod(
                "FormatTurnClock",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo normalize = typeof(PlaySessionController).GetMethod(
                "NormalizeTurnClock",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(format);
            Assert.NotNull(normalize);
            Assert.AreEqual("01:00", format.Invoke(null, new object[] { 60f }));
            Assert.AreEqual("00:10", format.Invoke(null, new object[] { 9.01f }));
            Assert.AreEqual("00:00", format.Invoke(null, new object[] { -2f }));
            Assert.AreEqual(0.5f, (float)normalize.Invoke(null, new object[] { 30f, 60f }), 0.001f);
            Assert.AreEqual(1f, (float)normalize.Invoke(null, new object[] { 90f, 60f }), 0.001f);
            Assert.AreEqual(0f, (float)normalize.Invoke(null, new object[] { 10f, 0f }), 0.001f);
        }

        [Test]
        public void CommandDrawer_InstantStatesControlVisibilityAndInput()
        {
            var root = new GameObject("Command Drawer Test", typeof(RectTransform));
            try
            {
                MatchCommandDrawerAnimator animator = root.AddComponent<MatchCommandDrawerAnimator>();
                Vector2 expanded = new(0f, 124f);
                Vector2 collapsed = new(0f, 48f);
                animator.Configure(expanded, collapsed, 0.3f);

                animator.SetExpandedInstant(false);
                Assert.IsFalse(animator.IsExpanded);
                Assert.AreEqual(collapsed, root.GetComponent<RectTransform>().anchoredPosition);
                Assert.AreEqual(0f, animator.Group.alpha, 0.001f);
                Assert.IsFalse(animator.Group.blocksRaycasts);
                Assert.IsFalse(animator.Group.interactable);

                animator.SetExpandedInstant(true);
                Assert.IsTrue(animator.IsExpanded);
                Assert.AreEqual(expanded, root.GetComponent<RectTransform>().anchoredPosition);
                Assert.AreEqual(1f, animator.Group.alpha, 0.001f);
                Assert.AreEqual(Vector3.one, root.transform.localScale);
                Assert.IsTrue(animator.Group.blocksRaycasts);
                Assert.IsTrue(animator.Group.interactable);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SettingsBackdrop_MenuUsesArtwork_MatchUsesTransparentScrim()
        {
            var root = new GameObject("Settings Test Root");
            try
            {
                SettingsMenuController menu = SettingsMenuController.Create(
                    root.transform,
                    GameFonts.Jersey25,
                    SettingsBackdropMode.MenuBackground);
                SettingsMenuController match = SettingsMenuController.Create(
                    root.transform,
                    GameFonts.Jersey25,
                    SettingsBackdropMode.LiveMatchOverlay);

                Assert.NotNull(menu.transform.Find("Settings Canvas/Background"));
                Assert.IsNull(match.transform.Find("Settings Canvas/Background"));

                Transform scrimTransform = match.transform.Find("Settings Canvas/Live Match Scrim");
                Assert.NotNull(scrimTransform);
                Image scrim = scrimTransform.GetComponent<Image>();
                Assert.That(scrim.color.a, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.IsTrue(scrim.raycastTarget);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConnectionAttemptGuard_BlocksRapidDuplicateActionsAndDisablesButtons()
        {
            var root = new GameObject("Connection Guard Test");
            try
            {
                var controller = root.AddComponent<PlaySessionController>();
                var ui = new UiFactory(GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Test Canvas", 0);
                SetPrivateField(controller, "_ui", ui);
                SetPrivateField(controller, "_text", new EchoTextProvider());
                SetPrivateField(controller, "_canvas", canvas);
                InvokePrivate(controller, "BuildHostSetup");

                MethodInfo begin = typeof(PlaySessionController).GetMethod(
                    "BeginConnectionAttempt", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(begin);
                Assert.IsTrue((bool)begin.Invoke(controller, null));
                Assert.IsFalse((bool)begin.Invoke(controller, null));

                Button create = canvas.transform.Find("Host Setup/Panel/Create Room").GetComponent<Button>();
                Assert.IsFalse(create.interactable);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HostScreen_BlankPlayerNameDoesNotStartConnectionAttempt()
        {
            var root = new GameObject("Name Validation Test");
            try
            {
                var controller = root.AddComponent<PlaySessionController>();
                var ui = new UiFactory(GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Test Canvas", 0);
                SetPrivateField(controller, "_ui", ui);
                SetPrivateField(controller, "_text", new EchoTextProvider());
                SetPrivateField(controller, "_canvas", canvas);
                InvokePrivate(controller, "BuildHostSetup");

                InputField field = canvas.transform.Find("Host Setup/Panel/PlayerName").GetComponent<InputField>();
                field.SetTextWithoutNotify("   ");
                MethodInfo commit = typeof(PlaySessionController).GetMethod(
                    "TryCommitPlayerName", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsFalse((bool)commit.Invoke(controller, new object[] { field, true }));

                FieldInfo pending = typeof(PlaySessionController).GetField(
                    "_connectionAttemptPending", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsFalse((bool)pending.GetValue(controller));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchBoard_UsesTwoPixelGuttersAndNoDuplicateSpriteInset()
        {
            var root = new GameObject("Board Test", typeof(RectTransform));
            try
            {
                var parent = root.GetComponent<RectTransform>();
                parent.sizeDelta = new Vector2(700f, 700f);
                var board = new MatchBoardView(new UiFactory(GameFonts.Jersey25), parent, (_, _) => { });

                RectTransform first = board.GetCellRect(0, 0);
                RectTransform next = board.GetCellRect(1, 0);
                RectTransform last = board.GetCellRect(GameRules.BoardSize - 1, GameRules.BoardSize - 1);

                Assert.AreEqual(46f, next.anchoredPosition.x - first.anchoredPosition.x, 0.001f);
                Assert.AreEqual(44f, first.sizeDelta.x, 0.001f);
                Assert.AreEqual(44f, first.sizeDelta.y, 0.001f);
                Assert.AreEqual(-first.anchoredPosition.x, last.anchoredPosition.x, 0.001f);
                Assert.AreEqual(-first.anchoredPosition.y, last.anchoredPosition.y, 0.001f);

                RectTransform icon = first.Find("TileIcon").GetComponent<RectTransform>();
                Assert.AreEqual(Vector2.zero, icon.offsetMin);
                Assert.AreEqual(Vector2.zero, icon.offsetMax);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TutorialBoard_UsesTheSameTileSpacingRule()
        {
            var root = new GameObject("Tutorial Board Test", typeof(RectTransform));
            try
            {
                var board = new MatchBoardView(
                    new UiFactory(GameFonts.Jersey25),
                    root.GetComponent<RectTransform>(),
                    (_, _) => { },
                    34f);

                RectTransform cell = board.GetCellRect(0, 0);
                Assert.AreEqual(32f, cell.sizeDelta.x, 0.001f);
                Assert.AreEqual(MatchBoardView.CellGap, 2f, 0.001f);
                RectTransform icon = cell.Find("TileIcon").GetComponent<RectTransform>();
                Assert.AreEqual(Vector2.zero, icon.offsetMin);
                Assert.AreEqual(Vector2.zero, icon.offsetMax);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryTileTypeHasAnImportedSprite()
        {
            for (byte tileId = 0; tileId < AMathTileSet.TileTypeCount; tileId++)
                Assert.NotNull(TileIcons.ForTile(tileId), $"Missing sprite for tile {tileId} ({AMathTileSet.SymbolOf(tileId)})");
        }

        [Test]
        public void HowToPlay_UsesReadableScrollableContentAndCompleteEquipmentIcons()
        {
            var root = new GameObject("How To Play Test", typeof(RectTransform));
            try
            {
                var overlay = new HowToPlayOverlay(new UiFactory(GameFonts.Jersey25), root.transform);
                Transform title = FindDescendant(root.transform, "Title");
                Transform heading = FindDescendant(root.transform, "Heading");
                Transform body = FindDescendant(root.transform, "Body");
                Transform viewport = FindDescendant(root.transform, "Content Viewport");
                Transform equalsIcon = FindDescendant(root.transform, "Icon =");

                Assert.AreEqual(72, title.GetComponent<Text>().fontSize);
                Assert.AreEqual(46, heading.GetComponent<Text>().fontSize);
                Assert.AreEqual(34, body.GetComponent<Text>().fontSize);
                Assert.NotNull(viewport.GetComponent<RectMask2D>());

                ScrollRect scroll = viewport.GetComponent<ScrollRect>();
                Assert.NotNull(scroll);
                Assert.IsTrue(scroll.vertical);
                Assert.IsFalse(scroll.horizontal);
                Assert.NotNull(equalsIcon);

                scroll.verticalNormalizedPosition = 0f;
                MethodInfo showPage = typeof(HowToPlayOverlay).GetMethod(
                    "ShowPage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                showPage.Invoke(overlay, new object[] { 1 });
                Assert.AreEqual(1f, scroll.verticalNormalizedPosition, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchResult_IsCenteredWithOneDarkBackdrop()
        {
            var root = new GameObject("Result Layout Test");
            try
            {
                var controller = root.AddComponent<PlaySessionController>();
                var ui = new UiFactory(GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Test Canvas", 0);
                SetPrivateField(controller, "_ui", ui);
                SetPrivateField(controller, "_text", new EchoTextProvider());
                SetPrivateField(controller, "_canvas", canvas);
                InvokePrivate(controller, "BuildResult");

                Transform result = canvas.transform.Find("Result");
                Image overlay = result.Find("Overlay").GetComponent<Image>();
                Assert.That(overlay.color.a, Is.GreaterThanOrEqualTo(0.8f));
                Transform content = result.Find("Result Content");
                Assert.NotNull(content.Find("WinnerLabel"));
                Assert.NotNull(content.Find("Body"));
                Assert.NotNull(content.Find("Rematch"));
                Assert.NotNull(content.Find("Leave"));
                RectTransform caption = content.Find("WinnerLabel").GetComponent<RectTransform>();
                RectTransform leave = content.Find("Leave").GetComponent<RectTransform>();
                float top = caption.anchoredPosition.y + caption.sizeDelta.y * 0.5f;
                float bottom = leave.anchoredPosition.y - leave.sizeDelta.y * 0.5f;
                float midpoint = content.GetComponent<RectTransform>().anchoredPosition.y
                    + (top + bottom) * 0.5f;
                Assert.That(Mathf.Abs(midpoint), Is.LessThan(20f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                Transform nested = FindDescendant(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static void SetPrivateField<TTarget, TValue>(TTarget target, string name, TValue value)
        {
            FieldInfo field = typeof(TTarget).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name);
            field.SetValue(target, value);
        }

        private static object GetPrivateField<TTarget>(TTarget target, string name)
        {
            FieldInfo field = typeof(TTarget).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name);
            return field.GetValue(target);
        }

        private static void InvokePrivate<TTarget>(TTarget target, string name, params object[] arguments)
        {
            MethodInfo method = typeof(TTarget).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, name);
            method.Invoke(target, arguments);
        }

        private static void AssertSeparatedHorizontally(RectTransform left, RectTransform right)
        {
            float leftEdge = left.anchoredPosition.x + left.sizeDelta.x * 0.5f;
            float rightEdge = right.anchoredPosition.x - right.sizeDelta.x * 0.5f;
            Assert.That(leftEdge, Is.LessThanOrEqualTo(rightEdge));
        }

        private static Rect ReferenceRect(RectTransform rect)
        {
            Vector2 reference = AdaptiveCanvasScaler.ReferenceResolution;
            Vector2 anchorPoint = Vector2.Scale(reference, rect.anchorMin);
            Vector2 minimum = anchorPoint + rect.anchoredPosition - Vector2.Scale(rect.sizeDelta, rect.pivot);
            return new Rect(minimum, rect.sizeDelta);
        }

        private static void AssertFitsScreen(RectTransform rect, Vector2 resolution)
        {
            Rect bounds = ReferenceRect(rect);
            float scale = resolution.x / AdaptiveCanvasScaler.ReferenceResolution.x;
            Assert.That(bounds.xMin * scale, Is.GreaterThanOrEqualTo(-0.01f), rect.name);
            Assert.That(bounds.yMin * scale, Is.GreaterThanOrEqualTo(-0.01f), rect.name);
            Assert.That(bounds.xMax * scale, Is.LessThanOrEqualTo(resolution.x + 0.01f), rect.name);
            Assert.That(bounds.yMax * scale, Is.LessThanOrEqualTo(resolution.y + 0.01f), rect.name);
        }

        private sealed class EchoTextProvider : ILocalizedTextProvider
        {
            public string GetText(string key) => key;
        }
    }
}
