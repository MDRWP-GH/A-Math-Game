using System;
using AMath.Art;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Reference-canvas layout slots shared by the normal match and tutorial.
    /// Keeping the interactive regions here makes accidental HUD overlap testable.
    /// </summary>
    internal static class MatchHudLayout
    {
        public const float RackPitch = 78f;
        public const int VisibleSeatCount = 4;
        public static readonly Vector2[] SeatAnchors =
        {
            new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f)
        };
        public static readonly Vector2[] SeatPivots =
        {
            new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f)
        };
        public static readonly Vector2[] SeatPositions =
        {
            new(28f, 28f), new(28f, -28f), new(-28f, -28f), new(-28f, 28f)
        };
        internal readonly struct Slot
        {
            public Slot(Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
            {
                Anchor = anchor;
                Pivot = pivot;
                Size = size;
                Position = position;
            }

            public Vector2 Anchor { get; }
            public Vector2 Pivot { get; }
            public Vector2 Size { get; }
            public Vector2 Position { get; }

            public void Apply(RectTransform rect) => UiFactory.SetAnchoredRect(
                rect,
                Anchor,
                Anchor,
                Pivot,
                Size,
                Position);

            public Rect InReferenceCanvas(Vector2 referenceResolution)
            {
                Vector2 anchorPoint = Vector2.Scale(referenceResolution, Anchor);
                Vector2 minimum = anchorPoint + Position - Vector2.Scale(Size, Pivot);
                return new Rect(minimum, Size);
            }
        }

        internal static class Standard
        {
            public const float BoardCellSize = 42f;
            public static readonly Slot Status = Top(new Vector2(720f, 40f), new Vector2(0f, -18f));
            public static readonly Slot Preview = Top(new Vector2(640f, 40f), new Vector2(0f, -60f));
            public static readonly Slot BoardFrame = Center(new Vector2(654f, 654f), new Vector2(0f, 90f));
            public static readonly Slot TilePreview = Center(new Vector2(220f, 280f), new Vector2(-530f, 200f));
            public static readonly Vector2 BoardSize = new(630f, 630f);
            public static readonly Slot Declare = Bottom(new Vector2(1000f, 80f), new Vector2(0f, 225f));
            public static readonly Slot Rack = Bottom(new Vector2(720f, 90f), new Vector2(0f, 140f));
            public static readonly Slot CommandToggle = Bottom(new Vector2(176f, 48f), new Vector2(-470f, 140f));
            public static readonly Slot CommandDrawer = Bottom(new Vector2(724f, 66f), new Vector2(0f, 48f));
            public static readonly Vector2 CommandDrawerCollapsedPosition = new(0f, -50f);
        }

        public static readonly Vector2 TutorialCoachSize = new(430f, 460f);

        private static Slot Top(Vector2 size, Vector2 position) =>
            new(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), size, position);

        private static Slot Bottom(Vector2 size, Vector2 position) =>
            new(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), size, position);

        private static Slot Center(Vector2 size, Vector2 position) =>
            new(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);
    }

    /// <summary>Visual pieces shared by the online match and scripted chapters.</summary>
    internal static class MatchHudElements
    {
        internal sealed class Seat
        {
            public GameObject Root;
            public Image Avatar;
            public Text Label;
            public Text Score;
            public Image TurnRing;
        }

        internal sealed class Timer
        {
            public Image Panel;
            public Text Caption;
            public Text Value;
            public Image Fill;
        }

        public static Timer CreateTimer(UiFactory ui, Transform parent, string caption, string initialValue)
        {
            var timer = new Timer();
            timer.Panel = UiFactory.CreateGlassPanel(parent, "Turn Timer", UiPalette.GlassStrong);
            UiFactory.SetAnchoredRect(timer.Panel.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(260f, 72f), new Vector2(28f, 0f));
            UiFactory.AddOutline(timer.Panel.gameObject,
                new Color(1f, 1f, 1f, 0.18f), new Vector2(1.5f, -1.5f));
            timer.Caption = ui.CreateText("Caption", timer.Panel.transform, caption,
                16, FontStyle.Bold, UiPalette.MutedText, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(timer.Caption.rectTransform, new Vector2(-82f, 4f), new Vector2(72f, 32f));
            timer.Value = ui.CreateText("Value", timer.Panel.transform, initialValue,
                38, FontStyle.Bold, UiPalette.Primary, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(timer.Value.rectTransform, new Vector2(34f, 4f), new Vector2(148f, 42f));
            UiFactory.AddDoubleOutline(timer.Value.gameObject,
                new Vector2(3f, -3f), new Vector2(1.5f, -1.5f));
            Image track = UiFactory.CreateImage("Track", timer.Panel.transform,
                new Color(UiPalette.Track.r, UiPalette.Track.g, UiPalette.Track.b, 0.34f));
            UiFactory.SetCenteredRect(track.rectTransform, new Vector2(0f, -25f), new Vector2(224f, 8f));
            timer.Fill = UiFactory.CreateImage("Fill", track.transform, UiPalette.Primary);
            UiFactory.Stretch(timer.Fill.rectTransform);
            timer.Fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            return timer;
        }

        public static Text CreateBag(UiFactory ui, Transform parent)
        {
            RectTransform root = UiFactory.CreateRect("Bag", parent);
            UiFactory.SetAnchoredRect(root,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(150f, 150f), new Vector2(-36f, 40f));
            Sprite bagSprite = TileIcons.Bag();
            Image icon = bagSprite != null
                ? UiFactory.CreateImage("Icon", root, bagSprite)
                : UiFactory.CreateImage("Icon", root, new Color(0.95f, 0.55f, 0.70f, 1f));
            icon.preserveAspect = true;
            UiFactory.SetCenteredRect(icon.rectTransform, new Vector2(0f, 10f), new Vector2(130f, 120f));
            Text count = ui.CreateText("Count", root, "0", 28, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(count.rectTransform, new Vector2(0f, -58f), new Vector2(120f, 36f));
            UiFactory.AddDoubleOutline(count.gameObject,
                new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));
            return count;
        }

        public static Image CreateBoardFrame(Transform parent)
        {
            Image frame = UiFactory.CreateImage("Board Frame", parent, UiPalette.BoardFrame);
            MatchHudLayout.Standard.BoardFrame.Apply(frame.rectTransform);
            UiFactory.AddShadow(frame.gameObject, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -10f));
            return frame;
        }

        public static Image CreateRackShelf(Transform parent)
        {
            Image shelf = UiFactory.CreateImage("Rack Shelf", parent, UiPalette.RackShelf);
            MatchHudLayout.Standard.Rack.Apply(shelf.rectTransform);
            UiFactory.AddShadow(shelf.gameObject, new Color(0f, 0f, 0f, 0.28f), new Vector2(0f, -6f));
            return shelf;
        }

        public static Button CreateAction(UiFactory ui, Transform parent, string name,
            string label, Color color, Color highlight, Action onClick, int index)
        {
            Button button = ui.CreateAccentButton(parent, name, label, color, highlight, onClick, 18);
            UiFactory.SetCenteredRect(button.GetComponent<RectTransform>(),
                new Vector2(-258f + index * 172f, 0f), new Vector2(160f, 50f));
            return button;
        }

        public static Seat CreateSeat(UiFactory ui, Transform parent, int index)
        {
            var seat = new Seat();
            RectTransform root = UiFactory.CreateRect($"Seat{index + 1}", parent);
            Vector2 anchor = MatchHudLayout.SeatAnchors[index];
            UiFactory.SetAnchoredRect(root, anchor, anchor, MatchHudLayout.SeatPivots[index],
                new Vector2(170f, 170f), MatchHudLayout.SeatPositions[index]);
            bool scoreBelow = anchor.y > 0.5f;
            float avatarY = scoreBelow ? 28f : -28f;
            float scoreY = scoreBelow ? -52f : 52f;

            seat.TurnRing = UiFactory.CreateImage("TurnRing", root, UiFactory.CircleSprite,
                new Color(UiPalette.TurnRing.r, UiPalette.TurnRing.g, UiPalette.TurnRing.b, 0f));
            UiFactory.SetCenteredRect(seat.TurnRing.rectTransform, new Vector2(0f, avatarY), new Vector2(126f, 126f));
            Image outline = UiFactory.CreateImage("AvatarOutline", root, UiFactory.CircleSprite,
                new Color(0f, 0f, 0f, 0.55f));
            UiFactory.SetCenteredRect(outline.rectTransform, new Vector2(0f, avatarY), new Vector2(112f, 112f));
            seat.Avatar = UiFactory.CreateImage("Avatar", root, UiFactory.CircleSprite, UiPalette.MutedText);
            UiFactory.SetCenteredRect(seat.Avatar.rectTransform, new Vector2(0f, avatarY), new Vector2(104f, 104f));
            UiFactory.AddShadow(seat.Avatar.gameObject, new Color(0f, 0f, 0f, 0.30f), new Vector2(0f, -4f));
            seat.Label = ui.CreateText("Label", root, $"P{index + 1}", 34, FontStyle.Bold,
                Color.black, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(seat.Label.rectTransform, new Vector2(0f, avatarY), new Vector2(100f, 48f));
            seat.Score = ui.CreateText("Score", root, string.Empty, 22, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(seat.Score.rectTransform, new Vector2(0f, scoreY), new Vector2(160f, 36f));
            UiFactory.AddDoubleOutline(seat.Score.gameObject, new Vector2(2.5f, -2.5f), new Vector2(1.2f, -1.2f));
            seat.Root = root.gameObject;
            seat.Root.SetActive(false);
            return seat;
        }

        public static Color ContrastingTextColor(Color background) =>
            background.r * 0.299f + background.g * 0.587f + background.b * 0.114f > 0.55f
                ? Color.black : Color.white;
    }
}
