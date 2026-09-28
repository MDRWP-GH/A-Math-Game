using AMath.Gameplay.Board;
using UnityEngine;

namespace AMath.Art
{
    /// <summary>
    /// Maps A-Math tile ids to the pixel-art sprites under Resources/Images/Icons.
    /// Falls back gracefully when an asset is missing so the HUD still works.
    /// </summary>
    public static class TileIcons
    {
        private static Sprite _normalizedEquals;

        public static Sprite ForTile(byte tileId)
        {
            string name = IconName(tileId);
            if (string.IsNullOrEmpty(name))
                return null;

            Sprite sprite = LoadIcon(name);
            if (tileId != AMathTileSet.EqualsSign || sprite == null)
                return sprite;

            // The equals artwork has a wider transparent border than the other
            // 28 tile assets. Crop that border once so every tile face occupies
            // the same proportion of its rack, board, drag and preview rect.
            if (_normalizedEquals == null)
            {
                Rect source = sprite.rect;
                const float visibleScale = 0.915f;
                float width = source.width * visibleScale;
                float height = source.height * visibleScale;
                var crop = new Rect(
                    source.center.x - width * 0.5f,
                    source.center.y - height * 0.5f,
                    width,
                    height);
                _normalizedEquals = Sprite.Create(
                    sprite.texture, crop, new Vector2(0.5f, 0.5f),
                    sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            return _normalizedEquals;
        }

        /// <summary>Bag art (multi-sprite sheet — prefers the large bag_0 slice).</summary>
        public static Sprite Bag()
        {
            Sprite[] slices = Resources.LoadAll<Sprite>("Images/Icons/bag");
            if (slices == null || slices.Length == 0)
                return GameImages.LoadIcon("bag");

            Sprite best = slices[0];
            for (int i = 1; i < slices.Length; i++)
            {
                if (slices[i].rect.width * slices[i].rect.height
                    > best.rect.width * best.rect.height)
                    best = slices[i];
            }

            return best;
        }

        public static Sprite LoadIcon(string name)
        {
            Sprite single = GameImages.LoadIcon(name);
            if (single != null)
                return single;

            Sprite[] slices = Resources.LoadAll<Sprite>("Images/Icons/" + name);
            return slices is { Length: > 0 } ? slices[0] : null;
        }

        private static string IconName(byte tileId)
        {
            if (tileId <= 20)
                return tileId.ToString();

            return tileId switch
            {
                AMathTileSet.Plus => "+",
                AMathTileSet.Minus => "-",
                AMathTileSet.PlusOrMinus => "±",
                AMathTileSet.Times => "x",
                AMathTileSet.Divide => "÷",
                AMathTileSet.TimesOrDivide => "×÷",
                AMathTileSet.EqualsSign => "=",
                AMathTileSet.Blank => "blank",
                _ => null
            };
        }
    }
}
