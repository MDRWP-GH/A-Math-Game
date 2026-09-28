using System.Reflection;
using System.Collections.Generic;
using AMath.Core.Commands;
using AMath.UI;
using AMath.Tutorial.UI;
using AMath.UI.Tutorial;
using AMath.UI.Localization;
using AMath.Tutorial.Localization;
using AMath.Settings;
using AMath.Gameplay.Board;
using AMath.Core.Events;
using AMath.Tutorial.Scripted;
using AMath.Tutorial.Definitions;
using UnityEngine.EventSystems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.Tests
{
    public sealed class TutorialUiTests
    {
        [Test]
        public void DismissActiveDialogue_CompletesAndHidesPanel()
        {
            var root = new GameObject("Tutorial UI");
            try
            {
                var ui = root.AddComponent<TutorialUI>();
                var panel = new GameObject("Dialogue Panel");
                SetPrivateField(ui, "_dialoguePanel", panel);

                int completed = 0;
                ui.Play("Dialogue", () => completed++);
                ui.DismissActiveDialogue();

                Assert.AreEqual(1, completed);
                Assert.IsFalse(panel.activeSelf);

                Object.DestroyImmediate(panel);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelectFirstInteractable_SkipsDisabledAndInactiveCandidates()
        {
            var root = new GameObject("Navigation Root");
            try
            {
                var disabled = new GameObject("Disabled", typeof(Button)).GetComponent<Button>();
                disabled.transform.SetParent(root.transform, false);
                disabled.interactable = false;

                var inactive = new GameObject("Inactive", typeof(Button)).GetComponent<Button>();
                inactive.transform.SetParent(root.transform, false);
                inactive.gameObject.SetActive(false);

                var enabled = new GameObject("Enabled", typeof(Button)).GetComponent<Button>();
                enabled.transform.SetParent(root.transform, false);

                Selectable selected = UiFactory.SelectFirstInteractable(disabled, inactive, enabled);

                Assert.AreSame(enabled, selected);
                Assert.IsTrue(selected.IsActive());
                Assert.IsTrue(selected.IsInteractable());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RackSpriteProviders_KeepTheirOwnRackIndex()
        {
            var textures = new Texture2D[4];
            var sprites = new Sprite[4];
            var providers = new System.Func<Sprite>[4];

            try
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    textures[i] = new Texture2D(1, 1);
                    sprites[i] = Sprite.Create(textures[i], new Rect(0, 0, 1, 1), Vector2.zero);
                    providers[i] = PlaySessionController.CaptureRackSpriteProvider(i, index => sprites[index]);
                }

                for (int i = 0; i < sprites.Length; i++)
                    Assert.AreSame(sprites[i], providers[i]());
            }
            finally
            {
                foreach (Sprite sprite in sprites)
                    if (sprite != null) Object.DestroyImmediate(sprite);
                foreach (Texture2D texture in textures)
                    if (texture != null) Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TilePreview_UsesFacePointsAndLocalizedNormalSquareText()
        {
            int originalLanguage = GameSettings.LanguageIndex;
            var root = new GameObject("Tile Preview Test");
            try
            {
                var ui = new UiFactory(AMath.Art.GameFonts.Jersey25);
                foreach (var provider in new AMath.Core.Assistance.ILocalizedTextProvider[]
                         { UiLocalizationProvider.Shared, new TutorialLocalizationProvider() })
                {
                    var panel = new TilePreviewPanel(ui, provider, root.transform,
                        MatchHudLayout.Standard.TilePreview);
                    foreach (int language in new[] { 0, 1 })
                    {
                        GameSettings.LanguageIndex = language;
                        foreach (byte tileId in new byte[] { AMathTileSet.Blank, 1, 4, 10 })
                        {
                            panel.Show(tileId);
                            Text points = root.transform.GetChild(root.transform.childCount - 1)
                                .Find("Tile Points").GetComponent<Text>();
                            string expected = string.Format(provider.GetText("ui.match.tile_preview_points"),
                                AMathTileSet.PointsOf(tileId));
                            Assert.AreEqual(expected, points.text);
                            Assert.IsFalse(points.text.Contains("ui.match.tile_preview_points"));
                        }
                    }
                    panel.Hide();
                }
            }
            finally
            {
                GameSettings.LanguageIndex = originalLanguage;
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TutorialFirstTile_CanBePlacedByClickOrDrag(bool drag)
        {
            var root = new GameObject("Tutorial Placement Test");
            var events = new GameObject("Pointer Events", typeof(EventSystem));
            var bus = new EventBus();
            using var host = new TutorialMatchHost(bus, new TutorialLocalizationProvider(),
                ScriptedTutorialMatchScript.Intro());
            try
            {
                bus.Publish(new TutorialStepChangedEvent
                {
                    TutorialId = IntroTutorialSequence.IntroTutorialId,
                    StepId = IntroTutorialSequence.Place1StepId,
                    StepIndex = 4, TotalSteps = 16, IsActive = true
                });
                var ui = new UiFactory(AMath.Art.GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Tutorial Test Canvas", 0);
                _ = new TutorialMatchView(ui, new TutorialLocalizationProvider(), host, canvas.transform);

                Button rack = canvas.transform.Find("Rack Shelf/Rack/R0").GetComponent<Button>();
                Button cell = canvas.transform.Find("Board Frame/Board/C5_7").GetComponent<Button>();
                if (drag)
                {
                    var pointer = new PointerEventData(events.GetComponent<EventSystem>());
                    rack.GetComponent<RackTileDragSource>().OnBeginDrag(pointer);
                    cell.GetComponent<BoardCellDropTarget>().OnDrop(pointer);
                    rack.GetComponent<RackTileDragSource>().OnEndDrag(pointer);
                }
                else
                {
                    rack.onClick.Invoke();
                    cell.onClick.Invoke();
                }

                Assert.AreEqual(1, host.Input.PendingPlacements.Count);
                Assert.AreEqual(5, host.Input.PendingPlacements[0].X);
                Assert.AreEqual(7, host.Input.PendingPlacements[0].Y);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(events);
            }
        }

        [TestCase("intro", false)]
        [TestCase("intro", true)]
        [TestCase("connect", false)]
        [TestCase("connect", true)]
        [TestCase("premium", false)]
        [TestCase("premium", true)]
        public void TutorialEveryPlacement_CanUseClickOrDrag(string chapter, bool drag)
        {
            var root = new GameObject("Tutorial All Placements Test");
            var events = new GameObject("Pointer Events", typeof(EventSystem));
            var bus = new EventBus();
            var text = new TutorialLocalizationProvider();
            ScriptedTutorialMatchScript script = chapter == "intro"
                ? ScriptedTutorialMatchScript.Intro()
                : chapter == "connect"
                    ? ScriptedTutorialMatchScript.Connect()
                    : ScriptedTutorialMatchScript.PremiumSkills();
            using var host = new TutorialMatchHost(bus, text, script);
            TutorialMatchView view = null;
            try
            {
                var ui = new UiFactory(AMath.Art.GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Tutorial Test Canvas", 0);
                view = new TutorialMatchView(ui, text, host, canvas.transform);
                var sequence = chapter == "intro"
                    ? (AMath.Tutorial.Interfaces.ITutorialSequenceDefinition)new IntroTutorialSequence(text)
                    : chapter == "connect"
                        ? new ConnectTutorialSequence(text)
                        : new PremiumTutorialSequence(text);

                int placements = 0;
                RackTileDragSource dragSource = null;
                PointerEventData pointer = null;
                for (int i = 0; i < sequence.Steps.Count; i++)
                {
                    string step = sequence.Steps[i].StepId;
                    if (!TutorialStepRouting.TryGetSelectTile(step, out byte tileId)
                        && !TutorialStepRouting.TryGetPlaceCell(step, out _, out _))
                        continue;

                    bus.Publish(new TutorialStepChangedEvent
                    {
                        TutorialId = sequence.TutorialId,
                        StepId = step,
                        StepIndex = i,
                        TotalSteps = sequence.Steps.Count,
                        IsActive = true
                    });

                    if (TutorialStepRouting.TryGetSelectTile(step, out tileId))
                    {
                        var rack = host.Players.GetById(host.Players.LocalPlayerId).Rack;
                        int rackIndex = -1;
                        for (int index = 0; index < rack.Count; index++)
                            if (rack[index] == tileId) { rackIndex = index; break; }
                        Assert.GreaterOrEqual(rackIndex, 0, step);
                        Button button = canvas.transform.Find($"Rack Shelf/Rack/R{rackIndex}").GetComponent<Button>();
                        Assert.IsTrue(button.interactable, step);
                        if (drag)
                        {
                            pointer = new PointerEventData(events.GetComponent<EventSystem>());
                            dragSource = button.GetComponent<RackTileDragSource>();
                            dragSource.OnBeginDrag(pointer);
                        }
                        else button.onClick.Invoke();
                    }
                    else if (TutorialStepRouting.TryGetPlaceCell(step, out int x, out int y))
                    {
                        Button cell = canvas.transform.Find($"Board Frame/Board/C{x}_{y}").GetComponent<Button>();
                        if (drag)
                        {
                            cell.GetComponent<BoardCellDropTarget>().OnDrop(pointer);
                            dragSource.OnEndDrag(pointer);
                        }
                        else cell.onClick.Invoke();
                        placements++;
                        Assert.AreEqual(placements, host.Input.PendingPlacements.Count, step);
                        Assert.AreEqual(x, host.Input.PendingPlacements[placements - 1].X, step);
                        Assert.AreEqual(y, host.Input.PendingPlacements[placements - 1].Y, step);
                    }
                }
                Assert.Greater(placements, 0, chapter);
            }
            finally
            {
                view?.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(events);
            }
        }

        [Test]
        public void TutorialPassAndExchangeButtons_CompleteBothLessons()
        {
            var root = new GameObject("Tutorial Commands Test");
            var bus = new EventBus();
            var resolved = new List<CommandType>();
            var rejected = new List<string>();
            bus.Subscribe<TurnResolvedEvent>(evt => resolved.Add((CommandType)evt.Record.CommandType));
            bus.Subscribe<CommandRejectedEvent>(evt => rejected.Add(evt.Reason));
            var text = new TutorialLocalizationProvider();
            using var host = new TutorialMatchHost(bus, text, ScriptedTutorialMatchScript.PremiumSkills());
            TutorialMatchView view = null;
            try
            {
                var ui = new UiFactory(AMath.Art.GameFonts.Jersey25);
                Canvas canvas = ui.CreateCanvas(root.transform, "Tutorial Test Canvas", 0);
                view = new TutorialMatchView(ui, text, host, canvas.transform);
                Button pass = canvas.transform.Find("Command Drawer/Pass").GetComponent<Button>();
                Button exchange = canvas.transform.Find("Command Drawer/Exchange").GetComponent<Button>();

                bus.Publish(new TutorialStepChangedEvent
                {
                    TutorialId = PremiumTutorialSequence.PremiumTutorialId,
                    StepId = PremiumTutorialSequence.PassStepId,
                    StepIndex = 12,
                    TotalSteps = 16,
                    IsActive = true
                });
                Assert.IsTrue(pass.interactable);
                pass.onClick.Invoke();
                CollectionAssert.AreEqual(new[] { CommandType.PassTurn }, resolved);

                bus.Publish(new TutorialStepChangedEvent
                {
                    TutorialId = PremiumTutorialSequence.PremiumTutorialId,
                    StepId = PremiumTutorialSequence.ExchangeIntroStepId,
                    StepIndex = 13,
                    TotalSteps = 16,
                    IsActive = true
                });
                bus.Publish(new TutorialStepChangedEvent
                {
                    TutorialId = PremiumTutorialSequence.PremiumTutorialId,
                    StepId = PremiumTutorialSequence.ExchangeStepId,
                    StepIndex = 14,
                    TotalSteps = 16,
                    IsActive = true
                });
                Assert.IsTrue(exchange.interactable);
                exchange.onClick.Invoke();
                foreach (int index in host.GuidedExchangeIndices)
                    canvas.transform.Find($"Rack Shelf/Rack/R{index}").GetComponent<Button>().onClick.Invoke();
                exchange.onClick.Invoke();

                CollectionAssert.AreEqual(new[] { CommandType.PassTurn, CommandType.ExchangeTiles }, resolved);
                Assert.IsEmpty(rejected);
            }
            finally
            {
                view?.Dispose();
                Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField<T>(TutorialUI target, string name, T value)
        {
            FieldInfo field = typeof(TutorialUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
