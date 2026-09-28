using AMath.UI;
using NUnit.Framework;
using UnityEngine;

namespace AMath.Tests
{
    public sealed class MatchHudLayoutTests
    {
        private static readonly Vector2 Reference = AdaptiveCanvasScaler.ReferenceResolution;

        [Test]
        public void StandardMatch_InteractiveZonesDoNotOverlap()
        {
            Rect board = RectOf(MatchHudLayout.Standard.BoardFrame);
            Rect tilePreview = RectOf(MatchHudLayout.Standard.TilePreview);
            Rect preview = RectOf(MatchHudLayout.Standard.Preview);
            Rect declare = RectOf(MatchHudLayout.Standard.Declare);
            Rect rack = RectOf(MatchHudLayout.Standard.Rack);
            Rect commands = RectOf(MatchHudLayout.Standard.CommandDrawer);
            Rect toggle = RectOf(MatchHudLayout.Standard.CommandToggle);

            AssertNoOverlap(board, preview, "board / preview");
            AssertNoOverlap(board, tilePreview, "board / tile close-up");
            AssertNoOverlap(rack, tilePreview, "rack / tile close-up");
            AssertNoOverlap(toggle, tilePreview, "command toggle / tile close-up");
            AssertNoOverlap(board, declare, "board / declaration selector");
            AssertNoOverlap(board, rack, "board / rack");
            AssertNoOverlap(declare, rack, "declaration selector / rack");
            AssertNoOverlap(rack, commands, "rack / command drawer");
            AssertNoOverlap(rack, toggle, "rack / command toggle");
        }

        [Test]
        public void TutorialCoach_InSideLane_DoesNotCoverBoardOrCommands()
        {
            Vector2 size = MatchHudLayout.TutorialCoachSize;
            Rect coach = new Rect(Reference.x - size.x - 24f,
                (Reference.y - size.y) * 0.5f, size.x, size.y);
            Rect board = RectOf(MatchHudLayout.Standard.BoardFrame);
            Rect rack = RectOf(MatchHudLayout.Standard.Rack);
            Rect commands = RectOf(MatchHudLayout.Standard.CommandDrawer);
            AssertNoOverlap(board, coach, "board / coach");
            AssertNoOverlap(rack, coach, "rack / coach");
            AssertNoOverlap(commands, coach, "commands / coach");
        }

        private static Rect RectOf(MatchHudLayout.Slot slot) => slot.InReferenceCanvas(Reference);

        private static void AssertNoOverlap(Rect first, Rect second, string label) =>
            Assert.IsFalse(first.Overlaps(second), label);
    }
}
