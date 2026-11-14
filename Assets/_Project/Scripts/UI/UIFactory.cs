using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GravityWells.UI
{
    // Builds the handful of widget types every screen is made from. No prefabs, no
    // textures from disk: everything is created in code, per CLAUDE.md.
    public static class UIFactory
    {
        private static Sprite _starSprite;
        private static Sprite _circleSprite;
        private static Sprite _whiteSprite;
        private static Sprite _roundedSprite;

        private const int RoundedSize = 96;
        private const int RoundedRadius = 34;
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.26f);

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Anchors and pivots to the same point so an element's position reads as "this
        // far in from that edge/center", which is how screens are laid out below.
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image CreateBackground(Transform parent, string name, UIStyle style)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>();
            style.RegisterBackground(image);
            return image;
        }

        public static Image CreateSurface(Transform parent, string name, UIStyle style)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            MakeRounded(image);
            style.RegisterSurface(image);
            return image;
        }

        // A dialog card: a touch lighter than the page but darker than the surface-colored
        // buttons placed on it, so both the card and its buttons read.
        public static Image CreateCard(Transform parent, string name, UIStyle style)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            MakeRounded(image);
            AddShadow(image);
            style.RegisterCard(image);
            return image;
        }

        // A rounded panel in a color the caller sets (a swatch, a preview): unlike a surface it is
        // not recolored when the theme changes, because its color is the content.
        public static Image CreateRoundedFlat(Transform parent, string name, Color color)
        {
            Image image = CreateFlat(parent, name, color);
            MakeRounded(image);
            return image;
        }

        // A flat, untinted color (e.g. the dim backdrop behind a dialog) that doesn't
        // follow the theme.
        public static Image CreateFlat(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(
            Transform parent, string name, string content, float fontSize, TextAlignmentOptions alignment,
            UIStyle style, bool dim = false, bool accent = false, bool bold = false)
        {
            RectTransform rect = CreateRect(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            // "Bold" means the display face: it is already heavy, so no synthetic bolding on top.
            TMP_FontAsset font = bold ? UIFonts.Display : UIFonts.Body;
            if (font != null)
            {
                text.font = font;
            }
            style.RegisterText(text, dim, accent);
            return text;
        }

        public static Button CreateButton(
            Transform parent, string name, string label, Vector2 size, UIStyle style,
            UnityAction onClick, bool primary = false, float fontSize = 44f, Sprite icon = null)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            MakeRounded(image);
            AddShadow(image);
            var button = rect.gameObject.AddComponent<Button>();
            rect.gameObject.AddComponent<PressFeedback>().Init(button, style);
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            TextMeshProUGUI text = CreateText(
                rect, "Label", label, fontSize, TextAlignmentOptions.Center, style, bold: primary);
            Stretch(text.rectTransform);

            Image iconImage = null;
            if (icon != null)
            {
                // Icon-only buttons center the glyph; with a label it sits in a left gutter and the
                // text is centered in what remains.
                bool iconOnly = string.IsNullOrEmpty(label);
                float iconSize = Mathf.Round(size.y * (iconOnly ? 0.5f : 0.42f));
                iconImage = CreateIconImage(rect, "Icon", icon, iconSize);
                if (iconOnly)
                {
                    Place(iconImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
                }
                else
                {
                    Place(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(iconSize, iconSize));
                    text.rectTransform.offsetMin = new Vector2(28f + iconSize, 0f);
                }
            }

            style.RegisterButton(image, text, primary, iconImage);

            return button;
        }

        // A horizontal 0..1 slider with a track the caller can paint (a hue rainbow, a brightness
        // ramp) and a round handle. Built from images only, like every other widget here.
        public static Slider CreateSlider(
            Transform parent, string name, Vector2 size, UIStyle style, UnityAction<float> onChange, out Image track)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = size;

            RectTransform trackRect = CreateRect(rect, "Track");
            Stretch(trackRect);
            track = trackRect.gameObject.AddComponent<Image>();
            track.color = Color.white;

            float handleSize = size.y + 18f;
            RectTransform area = CreateRect(rect, "HandleArea");
            Stretch(area);
            area.offsetMin = new Vector2(handleSize * 0.5f, 0f);
            area.offsetMax = new Vector2(-handleSize * 0.5f, 0f);

            RectTransform handle = CreateRect(area, "Handle");
            // The slider stretches the handle vertically across the track, so its height is the extra beyond it.
            handle.sizeDelta = new Vector2(handleSize, handleSize - size.y);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = CircleSprite;
            handleImage.color = Color.white;
            var ring = handle.gameObject.AddComponent<Outline>();
            ring.effectColor = new Color(0f, 0f, 0f, 0.55f);
            ring.effectDistance = new Vector2(3f, -3f);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.transition = Selectable.Transition.None;
            if (onChange != null)
            {
                slider.onValueChanged.AddListener(onChange);
            }
            return slider;
        }

        // A one-line text box that uppercases what is typed. TMP_InputField needs a viewport, a text
        // component and a placeholder, which are wired here.
        public static TMP_InputField CreateInputField(
            Transform parent, string name, Vector2 size, string placeholder, UIStyle style, int characterLimit)
        {
            Image background = CreateSurface(parent, name, style);
            background.rectTransform.sizeDelta = size;

            RectTransform viewport = CreateRect(background.transform, "Viewport");
            Stretch(viewport);
            viewport.offsetMin = new Vector2(30f, 10f);
            viewport.offsetMax = new Vector2(-30f, -10f);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = CreateText(viewport, "Text", string.Empty, 54f, TextAlignmentOptions.Left, style);
            Stretch(text.rectTransform);
            TextMeshProUGUI hint = CreateText(viewport, "Placeholder", placeholder, 48f, TextAlignmentOptions.Left, style, dim: true);
            Stretch(hint.rectTransform);

            var input = background.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onValidateInput = (current, index, typed) => char.ToUpperInvariant(typed);
            return input;
        }

        // A one-row gradient sprite from a colour function of position 0..1.
        public static Sprite CreateGradientSprite(System.Func<float, Color> colorAt, int width = 128)
        {
            var texture = new Texture2D(width, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int x = 0; x < width; x++)
            {
                Color color = colorAt(x / (float)(width - 1));
                texture.SetPixel(x, 0, color);
                texture.SetPixel(x, 1, color);
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, 2), new Vector2(0.5f, 0.5f), 100f);
        }

        // A white glyph that follows the theme's text color (or its accent).
        public static Image CreateIcon(Transform parent, string name, Sprite sprite, float size, UIStyle style, bool accent = false)
        {
            Image image = CreateIconImage(parent, name, sprite, size);
            style.RegisterIcon(image, accent);
            return image;
        }

        // An icon that keeps its own colors (the coin), so it is not registered for theme tinting.
        public static Image CreateIconImage(Transform parent, string name, Sprite sprite, float size)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        // Rounded corners from one 9-sliced sprite, so every button and card shares a radius.
        public static void MakeRounded(Image image)
        {
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        public static void AddShadow(Image image)
        {
            var shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = new Vector2(0f, -7f);
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite == null)
                {
                    _roundedSprite = BuildRoundedSprite();
                }
                return _roundedSprite;
            }
        }

        private static Sprite BuildRoundedSprite()
        {
            var texture = new Texture2D(RoundedSize, RoundedSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[RoundedSize * RoundedSize];
            float half = RoundedSize * 0.5f;
            float inner = half - RoundedRadius;
            for (int y = 0; y < RoundedSize; y++)
            {
                for (int x = 0; x < RoundedSize; x++)
                {
                    // Signed distance to a rounded square: one pixel of falloff gives clean antialiasing.
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - RoundedRadius;
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f - distance));
                    pixels[y * RoundedSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            float border = RoundedRadius + 2f;
            return Sprite.Create(
                texture, new Rect(0, 0, RoundedSize, RoundedSize), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static TMP_Text LabelOf(Button button)
        {
            return button.GetComponentInChildren<TMP_Text>();
        }

        // Flat white square, tinted by code: edges and bars in the diagrams.
        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    var pixels = new Color32[16];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = new Color32(255, 255, 255, 255);
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    _whiteSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                }
                return _whiteSprite;
            }
        }

        // An antialiased disc, for dots and indicators.
        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite == null)
                {
                    _circleSprite = Game.CircleSprite.Create(64);
                }
                return _circleSprite;
            }
        }

        // A five-point star drawn into a texture at first use — no art asset needed.
        public static Sprite StarSprite
        {
            get
            {
                if (_starSprite == null)
                {
                    _starSprite = BuildStarSprite(128);
                }
                return _starSprite;
            }
        }

        private static Sprite BuildStarSprite(int size)
        {
            var points = new Vector2[10];
            float center = size * 0.5f;
            float outer = size * 0.48f;
            float inner = outer * 0.42f;
            for (int i = 0; i < 10; i++)
            {
                float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(center + Mathf.Cos(angle) * radius, center + Mathf.Sin(angle) * radius);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            const int samples = 3;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            var p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                            if (PointInPolygon(p, points))
                            {
                                inside++;
                            }
                        }
                    }
                    byte alpha = (byte)Mathf.RoundToInt(255f * inside / (samples * samples));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // Even-odd rule ray cast.
        public static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
