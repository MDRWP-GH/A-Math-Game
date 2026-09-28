using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AMath.UI
{
    /// <summary>
    /// Moves transparent portions of the two painted backgrounds. The original Image
    /// remains the UI hit target and is also the fallback if either layer is missing.
    /// </summary>
    internal sealed class BackgroundMotion : MonoBehaviour
    {
        private enum MotionKind { Cloud, Tree, Smoke }

        private readonly struct LayerSpec
        {
            public readonly string Name;
            public readonly Rect Pixels;
            public readonly MotionKind Kind;
            public readonly float Period;
            public readonly float Phase;
            public readonly float Amplitude;

            public LayerSpec(string name, float left, float top, float right, float bottom,
                MotionKind kind, float period, float phase, float amplitude)
            {
                Name = name;
                Pixels = Rect.MinMaxRect(left, top, right, bottom);
                Kind = kind;
                Period = period;
                Phase = phase;
                Amplitude = amplitude;
            }
        }

        private sealed class MovingLayer
        {
            public RawImage Image;
            public LayerSpec Spec;
        }

        private const float AtlasWidth = 1672f;
        private const float AtlasHeight = 941f;
        private const string AnimatedRoot = "Images/Backgrounds/Animated/";

        private static readonly LayerSpec[] MenuLayers =
        {
            new LayerSpec("Left Clouds", 70, 28, 790, 202, MotionKind.Cloud, 23f, 0f, 0.018f),
            new LayerSpec("High Clouds", 870, 20, 1235, 158, MotionKind.Cloud, 27f, 0.28f, 0.014f),
            new LayerSpec("Right Clouds", 1240, 0, 1672, 205, MotionKind.Cloud, 20f, 0.52f, 0.016f),
            new LayerSpec("Horizon Clouds", 440, 202, 1170, 318, MotionKind.Cloud, 31f, 0.7f, 0.012f),
            new LayerSpec("Left Foreground Pine", 0, 290, 245, 941, MotionKind.Tree, 6.2f, 0f, 0.8f),
            new LayerSpec("Right Foreground Pine", 1420, 290, 1672, 941, MotionKind.Tree, 7.1f, 0.4f, 0.75f),
            new LayerSpec("Chimney Smoke A", 1245, 210, 1380, 405, MotionKind.Smoke, 5.4f, 0f, 0.045f),
            new LayerSpec("Chimney Smoke B", 1245, 210, 1380, 405, MotionKind.Smoke, 5.4f, 1f / 3f, 0.045f),
            new LayerSpec("Chimney Smoke C", 1245, 210, 1380, 405, MotionKind.Smoke, 5.4f, 2f / 3f, 0.045f)
        };

        private static readonly LayerSpec[] MatchLayers =
        {
            new LayerSpec("Upper Left Clouds", 0, 65, 435, 355, MotionKind.Cloud, 23f, 0f, 0.018f),
            new LayerSpec("Small Left Clouds", 445, 155, 680, 310, MotionKind.Cloud, 29f, 0.37f, 0.014f),
            new LayerSpec("Upper Right Clouds", 1170, 165, 1470, 370, MotionKind.Cloud, 26f, 0.6f, 0.015f),
            new LayerSpec("Edge Clouds", 1450, 0, 1672, 215, MotionKind.Cloud, 31f, 0.12f, 0.012f),
            new LayerSpec("Middle Left Clouds", 325, 375, 545, 525, MotionKind.Cloud, 21f, 0.5f, 0.014f),
            new LayerSpec("Middle Clouds", 950, 380, 1175, 510, MotionKind.Cloud, 24f, 0.8f, 0.013f),
            new LayerSpec("Left Treetops", 185, 480, 335, 785, MotionKind.Tree, 6.7f, 0.15f, 0.8f),
            new LayerSpec("Right Treetops", 1410, 470, 1672, 790, MotionKind.Tree, 7.5f, 0.55f, 0.75f)
        };

        private readonly List<MovingLayer> _layers = new List<MovingLayer>();
        private RectTransform _backgroundRect;
        private double _elapsed;

        internal static bool TryAttach(Image background, string resourceName)
        {
            string prefix;
            LayerSpec[] specs;
            if (resourceName == "Main Menu Backgrounds")
            {
                prefix = "Menu";
                specs = MenuLayers;
            }
            else if (resourceName == "In Game Backgrounds")
            {
                prefix = "Match";
                specs = MatchLayers;
            }
            else
            {
                return false;
            }

            if (background == null || background.sprite == null)
                return false;

            Sprite plateSprite = Resources.Load<Sprite>(AnimatedRoot + prefix + " Plate");
            Sprite atlasSprite = Resources.Load<Sprite>(AnimatedRoot + prefix + " Layers");
            if (plateSprite == null || atlasSprite == null)
                return false;

            Texture2D plate = plateSprite.texture;
            Texture2D atlas = atlasSprite.texture;

            plate.filterMode = FilterMode.Point;
            atlas.filterMode = FilterMode.Point;
            plate.wrapMode = TextureWrapMode.Clamp;
            atlas.wrapMode = TextureWrapMode.Clamp;

            BackgroundMotion motion = background.gameObject.AddComponent<BackgroundMotion>();
            motion._backgroundRect = background.rectTransform;
            motion.AddImage("Clean Plate", plate, new Rect(0f, 0f, 1f, 1f),
                new Rect(0f, 0f, 1f, 1f));
            foreach (LayerSpec spec in specs)
                motion.AddMovingLayer(atlas, spec);
            motion.ApplyAt(0.0);

            // Keep the original Image alive for TilePreviewBackground and as a fallback
            // if the two generated textures fail to load on another installation.
            background.color = new Color(1f, 1f, 1f, 0f);
            return true;
        }

        private RawImage AddImage(string name, Texture texture, Rect uv, Rect anchors)
        {
            var layerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            layerObject.transform.SetParent(transform, false);

            RawImage image = layerObject.GetComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchors.min;
            rect.anchorMax = anchors.max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private void AddMovingLayer(Texture2D atlas, LayerSpec spec)
        {
            Rect pixel = spec.Pixels;
            var uv = new Rect(pixel.xMin / AtlasWidth, 1f - pixel.yMax / AtlasHeight,
                pixel.width / AtlasWidth, pixel.height / AtlasHeight);
            var anchors = uv;
            RawImage image = AddImage(spec.Name, atlas, uv, anchors);
            if (spec.Kind == MotionKind.Tree || spec.Kind == MotionKind.Smoke)
                image.rectTransform.pivot = new Vector2(0.5f, 0f);
            _layers.Add(new MovingLayer { Image = image, Spec = spec });
        }

        private void LateUpdate()
        {
            _elapsed += Time.unscaledDeltaTime;
            ApplyAt(_elapsed);
        }

        internal void ApplyAt(double elapsed)
        {
            if (_backgroundRect == null)
                return;

            float width = _backgroundRect.rect.width;
            float height = _backgroundRect.rect.height;
            foreach (MovingLayer layer in _layers)
            {
                LayerSpec spec = layer.Spec;
                RectTransform rect = layer.Image.rectTransform;
                float cycle = (float)((elapsed / spec.Period) % 1.0) + spec.Phase;
                switch (spec.Kind)
                {
                    case MotionKind.Cloud:
                        rect.anchoredPosition = new Vector2(
                            Mathf.Sin(cycle * Mathf.PI * 2f) * spec.Amplitude * width, 0f);
                        break;
                    case MotionKind.Tree:
                        rect.localRotation = Quaternion.Euler(0f, 0f,
                            Mathf.Sin(cycle * Mathf.PI * 2f) * spec.Amplitude);
                        break;
                    case MotionKind.Smoke:
                        float progress = Mathf.Repeat(cycle, 1f);
                        rect.anchoredPosition = new Vector2(
                            Mathf.Sin(progress * Mathf.PI) * width * 0.002f,
                            progress * height * spec.Amplitude);
                        float scale = Mathf.Lerp(0.28f, 0.68f, progress);
                        rect.localScale = new Vector3(scale, scale, 1f);
                        Color tint = layer.Image.color;
                        tint.a = Mathf.Sin(progress * Mathf.PI) * 0.7f;
                        layer.Image.color = tint;
                        break;
                }
            }
        }
    }
}
