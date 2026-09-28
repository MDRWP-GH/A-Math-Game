using System;
using AMath.Art;
using AMath.Core.Assistance;
using AMath.Gameplay.Board;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>Read-only close-up of one physical A-Math tile.</summary>
    internal sealed class TilePreviewPanel
    {
        private readonly GameObject _root;
        private readonly Image _tileImage;
        private readonly Text _fallbackSymbol;
        private readonly Text _points;
        private readonly ILocalizedTextProvider _text;
        private byte? _tileId;

        public TilePreviewPanel(UiFactory ui, ILocalizedTextProvider text, Transform parent, MatchHudLayout.Slot slot)
        {
            _text = text;
            RectTransform root = UiFactory.CreateRect("Tile Preview", parent);
            slot.Apply(root);
            _root = root.gameObject;

            Image panel = UiFactory.CreateImage("Panel", root, UiPalette.GlassStrong);
            UiFactory.Stretch(panel.rectTransform);
            UiFactory.AddOutline(panel.gameObject, UiPalette.FieldBorder, new Vector2(2f, -2f));
            UiFactory.AddShadow(panel.gameObject, new Color(0f, 0f, 0f, 0.4f), new Vector2(0f, -8f));

            _tileImage = UiFactory.CreateImage("Large Tile", root, Color.white);
            _tileImage.preserveAspect = true;
            UiFactory.SetCenteredRect(_tileImage.rectTransform, new Vector2(0f, 27f), new Vector2(172f, 172f));

            _fallbackSymbol = ui.CreateText(
                "Fallback Symbol", root, string.Empty, 72, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_fallbackSymbol.rectTransform, new Vector2(0f, 27f), new Vector2(172f, 172f));

            _points = ui.CreateText(
                "Tile Points", root, string.Empty, 21, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetCenteredRect(_points.rectTransform, new Vector2(0f, -102f), new Vector2(196f, 68f));
            _points.horizontalOverflow = HorizontalWrapMode.Wrap;
            _root.SetActive(false);
        }

        public void Show(byte tileId)
        {
            if (!AMathTileSet.IsValidTileId(tileId))
            {
                Hide();
                return;
            }

            _tileId = tileId;
            Sprite sprite = TileIcons.ForTile(tileId);
            _tileImage.sprite = sprite;
            _tileImage.enabled = sprite != null;
            _fallbackSymbol.text = sprite == null ? AMathTileSet.SymbolOf(tileId) : string.Empty;
            RefreshLanguage();
            _root.transform.SetAsLastSibling();
            _root.SetActive(true);
        }

        public void RefreshLanguage()
        {
            if (!_tileId.HasValue) return;
            _points.text = string.Format(
                _text.GetText("ui.match.tile_preview_points"),
                AMathTileSet.PointsOf(_tileId.Value));
        }

        public void Hide()
        {
            _tileId = null;
            _root.SetActive(false);
        }
    }

    /// <summary>Dismisses the close-up only when an unoccupied background receives the click.</summary>
    internal sealed class TilePreviewBackground : MonoBehaviour, IPointerClickHandler
    {
        private Action _dismiss;

        public void Configure(Action dismiss) => _dismiss = dismiss;

        public void OnPointerClick(PointerEventData eventData) => _dismiss?.Invoke();
    }

    /// <summary>Lets a rack tile open its close-up even while its Button is disabled.</summary>
    internal sealed class TilePreviewRackTarget : MonoBehaviour, IPointerClickHandler
    {
        private Func<byte?> _tileId;
        private Action<byte> _show;

        public void Configure(Func<byte?> tileId, Action<byte> show)
        {
            _tileId = tileId;
            _show = show;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            byte? tileId = _tileId?.Invoke();
            if (tileId.HasValue)
                _show?.Invoke(tileId.Value);
        }
    }
}
