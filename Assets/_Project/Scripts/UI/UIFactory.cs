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
        private static Sprite _discSprite;

        private const int RoundedSize = 96;
        private const int RoundedRadius = 42; // UITokens.CornerRadius (the sprite is slice-scaled, so this is its fixed corner)
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
            UnityAction onClick, bool primary = false, float fontSize = 44f, Sprite icon = null, bool tintIcon = true)
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
                float iconSize = IconSizeFor(size, iconOnly);
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

            // An icon with colours of its own (the coin) is left out, or the theme would paint it flat.
            style.RegisterButton(image, text, primary, tintIcon ? iconImage : null);

            return button;
        }

        public static float IconSizeFor(Vector2 buttonSize, bool iconOnly)
        {
            return Mathf.Round(buttonSize.y * (iconOnly ? 0.5f : 0.42f));
        }

        // A panel one level above a surface (a tile or a cell sitting on a card): the palette's elevated
        // colour, rounded, with no shadow of its own.
        public static Image CreateElevated(Transform parent, string name, UIStyle style)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            MakeRounded(image);
            style.RegisterCard(image);
            return image;
        }

        // The title line, and under it a smaller supporting line, for a tile. One rich-text string so the
        // button's single label (and its theme colour) carries both.
        public static string TileText(string title, string subtitle)
        {
            return string.IsNullOrEmpty(subtitle)
                ? title
                : title + "\n<size=74%><alpha=#B8>" + subtitle + "</size>";
        }

        // A wide, left-aligned button: icon, a title and an optional supporting line. The same widget serves
        // as the main call to action (primary) and as a secondary card, so every menu entry shares one shape.
        public static Button CreateTile(
            Transform parent, string name, string title, string subtitle, Vector2 size, UIStyle style,
            UnityAction onClick, bool primary, Sprite icon, float titleSize)
        {
            Button button = CreateButton(parent, name, TileText(title, subtitle), size, style, onClick, primary, titleSize, icon);
            TMP_Text label = LabelOf(button);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            float gutter = icon != null ? 28f + IconSizeFor(size, false) + UITokens.SpaceM : UITokens.SpaceL;
            label.rectTransform.offsetMin = new Vector2(gutter, 0f);
            label.rectTransform.offsetMax = new Vector2(-UITokens.SpaceM, 0f);
            return button;
        }

        // A button with no fill, for things that must stay available but should not compete with the main
        // actions (Settings, How to play). Supporting-text colour, with a press squash like every button.
        public static Button CreateQuietButton(
            Transform parent, string name, string label, Vector2 size, UIStyle style, UnityAction onClick, Sprite icon)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = size;

            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.canvasRenderer.cullTransparentMesh = false; // a fully transparent graphic must still take taps
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            rect.gameObject.AddComponent<PressFeedback>().Init(button, style);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            TextMeshProUGUI text = CreateText(
                rect, "Label", label, UITokens.TextButton * 0.92f, TextAlignmentOptions.Center, style, dim: true);
            Stretch(text.rectTransform);

            if (icon != null)
            {
                float iconSize = UITokens.IconSize;
                Image iconImage = CreateIconImage(rect, "Icon", icon, iconSize);
                style.RegisterDimIcon(iconImage);
                Place(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(UITokens.SpaceM, 0f), new Vector2(iconSize, iconSize));
                text.rectTransform.offsetMin = new Vector2(UITokens.SpaceM + iconSize, 0f);
            }
            return button;
        }

        // A small rounded chip for a reward or a price ("AD", "12") sitting on a button: the same shape
        // everywhere an action costs something, so a rewarded action never reads as a plain control.
        public static Image CreateBadge(Transform parent, string name, Vector2 size, UIStyle style, out TextMeshProUGUI label)
        {
            Image chip = CreateElevated(parent, name, style);
            chip.rectTransform.sizeDelta = size;
            chip.raycastTarget = false;
            label = CreateText(chip.rectTransform, "Text", string.Empty, UITokens.TextMeta + 2f, TextAlignmentOptions.Center, style, dim: true);
            Stretch(label.rectTransform);
            return chip;
        }

        // A small, quiet caption above a group ("EXPLORE", "YOUR STREAK").
        public static TextMeshProUGUI CreateSectionHeader(Transform parent, string name, string text, UIStyle style)
        {
            TextMeshProUGUI header = CreateText(
                parent, name, text.ToUpperInvariant(), UITokens.TextMeta + 1f, TextAlignmentOptions.MidlineLeft, style, dim: true);
            header.characterSpacing = 6f;
            return header;
        }

        // A circular icon-only button, for controls that are known by their glyph (Back, Pause, Undo):
        // a different shape from the slabs, so navigation chrome and actions stop looking alike.
        public static Button CreateRoundButton(
            Transform parent, string name, Sprite icon, float diameter, UIStyle style, UnityAction onClick, bool primary = false)
        {
            Button button = CreateButton(
                parent, name, string.Empty, new Vector2(diameter, diameter), style, onClick, primary, icon: icon);
            var fill = (Image)button.targetGraphic;
            fill.sprite = DiscSprite;
            fill.type = Image.Type.Simple;
            return button;
        }

        // An empty rounded card that is one big button; the caller lays its own content out on it.
        public static Button CreateCardButton(Transform parent, string name, Vector2 size, UIStyle style, UnityAction onClick)
        {
            return CreateButton(parent, name, string.Empty, size, style, onClick);
        }

        // One entry of a bottom navigation bar: a glyph with a small caption under it, no fill.
        public static Button CreateNavItem(
            Transform parent, string name, string label, Sprite icon, Vector2 size, UIStyle style, UnityAction onClick)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = size;
            Button button = AddClearHit(rect, style, onClick, squash: true);

            float iconSize = Mathf.Round(size.y * 0.42f);
            Image iconImage = CreateIcon(rect, "Icon", icon, iconSize, style);
            Place(iconImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.12f), new Vector2(iconSize, iconSize));

            TextMeshProUGUI text = CreateText(
                rect, "Label", label, UITokens.TextMeta - 4f, TextAlignmentOptions.Center, style, dim: true);
            Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, size.y * 0.08f), new Vector2(size.x, 40f));
            return button;
        }

        // A flat list row: a label on the left, whatever the caller puts in `accessory` on the right
        // (a switch, a value, a chevron) and a hairline under it. Rows sit together inside one card,
        // which is how settings read as a list rather than as a stack of identical buttons.
        public static Button CreateRow(
            Transform parent, string name, string label, Vector2 size, UIStyle style, UnityAction onClick,
            Sprite icon, bool divider, out RectTransform accessory, out TextMeshProUGUI labelText)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = size;
            Button button = AddClearHit(rect, style, onClick, squash: false);

            float left = UITokens.SpaceL;
            if (icon != null)
            {
                Image iconImage = CreateIcon(rect, "Icon", icon, UITokens.IconSize, style);
                Place(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(left, 0f), new Vector2(UITokens.IconSize, UITokens.IconSize));
                left += UITokens.IconSize + UITokens.SpaceM;
            }

            labelText = CreateText(rect, "Label", label, UITokens.TextButton, TextAlignmentOptions.MidlineLeft, style);
            Stretch(labelText.rectTransform);
            labelText.rectTransform.offsetMin = new Vector2(left, 0f);
            labelText.rectTransform.offsetMax = new Vector2(-UITokens.SpaceL, 0f);

            accessory = CreateRect(rect, "Accessory");
            Place(accessory, new Vector2(1f, 0.5f), new Vector2(-UITokens.SpaceL, 0f), new Vector2(0f, size.y));

            if (divider)
            {
                Image line = CreateFlat(rect, "Divider", Color.clear);
                line.raycastTarget = false;
                line.rectTransform.anchorMin = new Vector2(0f, 0f);
                line.rectTransform.anchorMax = new Vector2(1f, 0f);
                line.rectTransform.pivot = new Vector2(0.5f, 0f);
                line.rectTransform.offsetMin = new Vector2(UITokens.SpaceL, 0f);
                line.rectTransform.offsetMax = new Vector2(-UITokens.SpaceL, 3f);
                style.RegisterCustom(palette =>
                {
                    Color ink = palette.Text;
                    line.color = new Color(ink.r, ink.g, ink.b, 0.12f);
                });
            }
            return button;
        }

        // A dim value or caption on the right of a row ("Strong", "$2.99").
        public static TextMeshProUGUI CreateRowValue(RectTransform accessory, string text, UIStyle style)
        {
            TextMeshProUGUI value = CreateText(
                accessory, "Value", text, UITokens.TextSupporting, TextAlignmentOptions.MidlineRight, style, dim: true);
            Place(value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(420f, 60f));
            return value;
        }

        public static Image CreateRowChevron(RectTransform accessory, UIStyle style)
        {
            Image chevron = CreateIconImage(accessory, "Chevron", UIIcons.Get("chevron"), 40f);
            style.RegisterDimIcon(chevron);
            Place(chevron.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            return chevron;
        }

        // An invisible, tappable area with the usual tap sound.
        private static Button AddClearHit(RectTransform rect, UIStyle style, UnityAction onClick, bool squash)
        {
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.canvasRenderer.cullTransparentMesh = false; // a fully transparent graphic must still take taps
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            rect.gameObject.AddComponent<PressFeedback>().Init(button, style, squash);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
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

        // The same antialiased disc the board's nodes use, for dots in the diagrams.
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

        // A crisp disc for round buttons and switch knobs (the diagram disc is too small to scale up).
        public static Sprite DiscSprite
        {
            get
            {
                if (_discSprite == null)
                {
                    _discSprite = Game.CircleSprite.Create(192);
                }
                return _discSprite;
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
            float inner = outer * 0.54f; // a full, friendly star; the classic 0.38 ratio reads thin and spiky at this size
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
