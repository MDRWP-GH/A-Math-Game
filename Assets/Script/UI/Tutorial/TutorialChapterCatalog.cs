using System;
using System.Collections.Generic;
using AMath.Core.Assistance;
using AMath.Tutorial.Definitions;
using AMath.Tutorial.Interfaces;
using AMath.Tutorial.Save;
using AMath.Tutorial.Scripted;
using UnityEngine;

namespace AMath.UI.Tutorial
{
    internal enum TutorialChapterState
    {
        Locked,
        New,
        InProgress,
        Completed
    }

    /// <summary>UI-facing metadata and factories for one tutorial chapter.</summary>
    internal sealed class TutorialChapterDescriptor
    {
        public TutorialChapterDescriptor(
            string id,
            string cardTitleKey,
            string descriptionKey,
            string lockedDescriptionKey,
            string prerequisiteId,
            string[] outcomeKeys,
            Color accent,
            Color accentHighlight,
            Func<ILocalizedTextProvider, ITutorialSequenceDefinition> createSequence,
            Func<ScriptedTutorialMatchScript> createScript)
        {
            Id = id;
            CardTitleKey = cardTitleKey;
            DescriptionKey = descriptionKey;
            LockedDescriptionKey = lockedDescriptionKey;
            PrerequisiteId = prerequisiteId;
            OutcomeKeys = outcomeKeys ?? Array.Empty<string>();
            Accent = accent;
            AccentHighlight = accentHighlight;
            CreateSequence = createSequence;
            CreateScript = createScript;
        }

        public string Id { get; }
        public string CardTitleKey { get; }
        public string DescriptionKey { get; }
        public string LockedDescriptionKey { get; }
        public string PrerequisiteId { get; }
        public IReadOnlyList<string> OutcomeKeys { get; }
        public Color Accent { get; }
        public Color AccentHighlight { get; }
        public Func<ILocalizedTextProvider, ITutorialSequenceDefinition> CreateSequence { get; }
        public Func<ScriptedTutorialMatchScript> CreateScript { get; }

        public bool IsUnlocked(ITutorialSaveStore saveStore)
        {
            if (string.IsNullOrEmpty(PrerequisiteId))
                return true;

            return saveStore != null
                && saveStore.TryLoad(PrerequisiteId, out TutorialProgressData prerequisite)
                && prerequisite != null
                && prerequisite.IsCompleted;
        }

        public TutorialChapterState ResolveState(
            ITutorialSaveStore saveStore,
            out TutorialProgressData progress)
        {
            progress = null;
            if (!IsUnlocked(saveStore))
                return TutorialChapterState.Locked;

            if (saveStore == null || !saveStore.TryLoad(Id, out progress) || progress == null)
                return TutorialChapterState.New;

            return progress.IsCompleted
                ? TutorialChapterState.Completed
                : TutorialChapterState.InProgress;
        }
    }

    internal static class TutorialChapterCatalog
    {
        private static readonly TutorialChapterDescriptor[] Chapters =
        {
            new TutorialChapterDescriptor(
                IntroTutorialSequence.IntroTutorialId,
                "tutorial.chapters.intro",
                "tutorial.chapters.intro_desc",
                null,
                null,
                new[] { "tutorial.outcome.intro.1", "tutorial.outcome.intro.2", "tutorial.outcome.intro.3" },
                UiPalette.Success,
                UiPalette.SuccessHighlight,
                text => new IntroTutorialSequence(text),
                ScriptedTutorialMatchScript.Intro),
            new TutorialChapterDescriptor(
                ConnectTutorialSequence.ConnectTutorialId,
                "tutorial.chapters.connect",
                "tutorial.chapters.connect_desc",
                "tutorial.chapters.locked",
                IntroTutorialSequence.IntroTutorialId,
                new[] { "tutorial.outcome.connect.1", "tutorial.outcome.connect.2", "tutorial.outcome.connect.3" },
                UiPalette.Primary,
                UiPalette.PrimaryHighlight,
                text => new ConnectTutorialSequence(text),
                ScriptedTutorialMatchScript.Connect),
            new TutorialChapterDescriptor(
                PremiumTutorialSequence.PremiumTutorialId,
                "tutorial.chapters.premium",
                "tutorial.chapters.premium_desc",
                "tutorial.chapters.locked_connect",
                ConnectTutorialSequence.ConnectTutorialId,
                new[] { "tutorial.outcome.premium.1", "tutorial.outcome.premium.2", "tutorial.outcome.premium.3" },
                UiPalette.Secondary,
                UiPalette.SecondaryHighlight,
                text => new PremiumTutorialSequence(text),
                ScriptedTutorialMatchScript.PremiumSkills)
        };

        public static IReadOnlyList<TutorialChapterDescriptor> All => Chapters;

        public static int IndexOf(string tutorialId)
        {
            for (int i = 0; i < Chapters.Length; i++)
            {
                if (string.Equals(Chapters[i].Id, tutorialId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public static TutorialChapterDescriptor Find(string tutorialId)
        {
            int index = IndexOf(tutorialId);
            return index >= 0 ? Chapters[index] : Chapters[0];
        }

        public static TutorialChapterDescriptor Next(string tutorialId)
        {
            int index = IndexOf(tutorialId);
            return index >= 0 && index + 1 < Chapters.Length ? Chapters[index + 1] : null;
        }
    }
}
