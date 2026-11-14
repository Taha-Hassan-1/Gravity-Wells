using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GravityWells.Meta;

namespace GravityWells.UI
{
    // Owns the Canvas, the EventSystem and the toast, and routes the Android back button (Escape) to
    // whichever screen is current. Screens are built by the game phase that needs them and handed to
    // SwitchTo; the root knows nothing about any particular screen.
    public sealed class UIRoot : MonoBehaviour
    {
        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 1920f;
        private const float ToastSeconds = 2.2f;
        private const float ScreenFadeSeconds = 0.18f;

        private readonly List<RectTransform> _safeAreaRects = new List<RectTransform>();
        private readonly List<UIScreen> _screens = new List<UIScreen>();

        private UIStyle _style;
        private CanvasScaler _scaler;
        private UIScreen _current;
        private CanvasGroup _toastGroup;
        private CanvasGroup _fadingGroup;
        private float _fadeElapsed;
        private TextMeshProUGUI _toastText;
        private float _toastTimer;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        public Transform CanvasTransform { get; private set; }
        public UIStyle Style => _style;
        public UIScreen Current => _current;

        public void Init(ThemePalette initialPalette)
        {
            _style = new UIStyle(initialPalette);
            EnsureEventSystem();
            CanvasTransform = BuildCanvas();
            BuildToast(CanvasTransform);
            ApplyLayout(force: true);
        }

        public void RegisterSafeArea(RectTransform rect) => _safeAreaRects.Add(rect);

        // Every screen registers here so SwitchTo can hide the others.
        public void AddScreen(UIScreen screen) => _screens.Add(screen);

        public void ApplyTheme(ThemePalette palette)
        {
            if (_style != null)
            {
                _style.Apply(palette);
            }
        }

        public void ShowToast(string message)
        {
            _toastText.text = message;
            _toastTimer = ToastSeconds;
            _toastGroup.alpha = 1f;
        }

        public void SwitchTo(UIScreen screen)
        {
            HideAll();
            _current = screen;
            screen.Show();
            BeginFade(screen.Root);
        }

        // A short fade on arrival so screens don't pop. The result card has its own fade.
        private void BeginFade(GameObject root)
        {
            if (_fadingGroup != null)
            {
                _fadingGroup.alpha = 1f;
            }
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = root.AddComponent<CanvasGroup>();
            }
            group.alpha = 0f;
            _fadingGroup = group;
            _fadeElapsed = 0f;
        }

        private void TickFade(float unscaledDeltaTime)
        {
            if (_fadingGroup == null)
            {
                return;
            }
            _fadeElapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_fadeElapsed / ScreenFadeSeconds);
            _fadingGroup.alpha = t * t * (3f - 2f * t);
            if (t >= 1f)
            {
                _fadingGroup = null;
            }
        }

        private void HideAll()
        {
            foreach (UIScreen screen in _screens)
            {
                screen.Hide();
            }
        }

        // ---- per-frame ----

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && _current != null)
            {
                _current.OnBack();
            }

            TickFade(Time.unscaledDeltaTime);
            TickToast();
            ApplyLayout(force: false);
        }

        private void TickToast()
        {
            if (_toastTimer <= 0f)
            {
                return;
            }
            _toastTimer -= Time.unscaledDeltaTime;
            _toastGroup.alpha = Mathf.Clamp01(_toastTimer / 0.5f); // hold, then fade over the last half second
        }

        // Re-fits the safe area and the canvas scaling mode when the screen changes
        // (rotation-free here, but split-screen/foldables still resize the window).
        private void ApplyLayout(bool force)
        {
            Rect safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == _lastSafeArea && size == _lastScreenSize)
            {
                return;
            }
            _lastSafeArea = safe;
            _lastScreenSize = size;

            // Phones narrower than the reference aspect match on width (more height is
            // free); wider screens (tablets, the Editor's landscape Game view) match on
            // height so the 1920-unit-tall layouts never get cropped.
            float aspect = (float)size.x / Mathf.Max(1, size.y);
            _scaler.matchWidthOrHeight = aspect > ReferenceWidth / ReferenceHeight ? 1f : 0f;

            ComputeSafeAnchors(safe, size, out Vector2 min, out Vector2 max);
            foreach (RectTransform rect in _safeAreaRects)
            {
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        // Converts the device safe area to 0..1 anchors. A safe area that doesn't fit inside
        // the screen is bogus (the Editor's Simulator reports its device's geometry even
        // while the Game view is another size) and would push every screen off-canvas, so
        // it falls back to the full screen instead.
        public static void ComputeSafeAnchors(Rect safe, Vector2Int screenSize, out Vector2 min, out Vector2 max)
        {
            const float tolerance = 1f;
            bool valid = screenSize.x > 0 && screenSize.y > 0
                && safe.width > 0f && safe.height > 0f
                && safe.xMin >= -tolerance && safe.yMin >= -tolerance
                && safe.xMax <= screenSize.x + tolerance && safe.yMax <= screenSize.y + tolerance;

            if (!valid)
            {
                min = Vector2.zero;
                max = Vector2.one;
                return;
            }

            min = new Vector2(safe.xMin / screenSize.x, safe.yMin / screenSize.y);
            max = new Vector2(safe.xMax / screenSize.x, safe.yMax / screenSize.y);
        }

        // ---- construction ----

        private static void EnsureEventSystem()
        {
            // Without an EventSystem no button in the game would ever receive a click.
            if (EventSystem.current != null)
            {
                return;
            }
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private Transform BuildCanvas()
        {
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _scaler = canvasGO.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            canvasGO.AddComponent<GraphicRaycaster>();
            return canvasGO.transform;
        }

        private void BuildToast(Transform canvas)
        {
            RectTransform root = UIFactory.CreateRect(canvas, "Toast");
            UIFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 360f), new Vector2(900f, 90f));
            _toastGroup = root.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;

            Image pill = UIFactory.CreateSurface(root, "Pill", _style);
            UIFactory.Stretch(pill.rectTransform);

            _toastText = UIFactory.CreateText(root, "Text", string.Empty, 36f, TextAlignmentOptions.Center, _style);
            UIFactory.Stretch(_toastText.rectTransform);
        }
    }
}
