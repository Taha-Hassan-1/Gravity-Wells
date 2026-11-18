using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GravityWells.Core;
using GravityWells.Game;

namespace GravityWells.UI
{
    // The screen while a level is being played: level number and pause at the top, gates lit and wells
    // used under them, the well tray and the Launch button at the bottom, with Undo, Restart and Hint
    // beside them. It owns no rules; it shows what GameManager tells it and reports button presses.
    public sealed class GameHUD : UIScreen, IWellTray
    {
        private const float RoundButton = 108f;
        private const float SlotSize = 164f;

        private readonly UIStyle _style;
        private readonly Action _onBack;
        private readonly TextMeshProUGUI _level;
        private readonly TextMeshProUGUI _gates;
        private readonly TextMeshProUGUI _wells;
        private readonly Button _undo;
        private readonly Button _restart;
        private readonly Button _hint;
        private readonly Button _launch;
        private readonly RectTransform _trayStrip;
        private readonly RectTransform _attractorSlot;
        private readonly RectTransform _repulsorSlot;
        private readonly TextMeshProUGUI _attractorCount;
        private readonly TextMeshProUGUI _repulsorCount;

        private int _litShown = -1;
        private int _gatesShown = -1;
        private int _usedShown = -1;
        private int _parShown = -1;

        public GameHUD(
            UIRoot ui, UIStyle style, Action onPause, Action onUndo, Action onRestart, Action onHint, Action onLaunch)
        {
            _style = style;
            _onBack = onPause;
            BuildRoot(ui.CanvasTransform, "GameHUD", ui, style, opaque: false);

            // Top row: pause, the level, hint.
            Button pause = UIFactory.CreateRoundButton(Content, "Pause", UIIcons.Get("pause"), RoundButton, style, () => onPause());
            UIFactory.Place((RectTransform)pause.transform, new Vector2(0f, 1f), new Vector2(UITokens.SpaceM, -UITokens.SpaceM), new Vector2(RoundButton, RoundButton));

            _hint = UIFactory.CreateRoundButton(Content, "Hint", UIIcons.Get("hint"), RoundButton, style, () => onHint());
            UIFactory.Place((RectTransform)_hint.transform, new Vector2(1f, 1f), new Vector2(-UITokens.SpaceM, -UITokens.SpaceM), new Vector2(RoundButton, RoundButton));

            _level = UIFactory.CreateText(Content, "Level", string.Empty, 64f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(_level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -UITokens.SpaceM - 4f), new Vector2(520f, RoundButton));

            // Development builds only: seven quick taps on the level title run the device determinism check.
            if (Debug.isDebugBuild)
            {
                Image hit = UIFactory.CreateFlat(Content, "DebugHit", new Color(0f, 0f, 0f, 0f));
                UIFactory.Place(hit.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -UITokens.SpaceM), new Vector2(520f, RoundButton));
                hit.gameObject.AddComponent<Button>().onClick.AddListener(CountSecretTap);
            }

            // Under it, the two counters.
            _gates = UIFactory.CreateText(Content, "Gates", string.Empty, 44f, TextAlignmentOptions.Center, style);
            UIFactory.Place(_gates.rectTransform, new Vector2(0.25f, 1f), new Vector2(0f, -150f - UITokens.SpaceM), new Vector2(440f, 70f));
            _wells = UIFactory.CreateText(Content, "Wells", string.Empty, 44f, TextAlignmentOptions.Center, style);
            UIFactory.Place(_wells.rectTransform, new Vector2(0.75f, 1f), new Vector2(0f, -150f - UITokens.SpaceM), new Vector2(440f, 70f));

            // The bottom strip is the tray: dropping a well anywhere on it removes the well.
            _trayStrip = UIFactory.CreateRect(Content, "TrayStrip");
            UIFactory.Place(_trayStrip, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1080f, 410f));
            _trayStrip.anchorMin = new Vector2(0f, 0f);
            _trayStrip.anchorMax = new Vector2(1f, 0f);
            _trayStrip.pivot = new Vector2(0.5f, 0f);
            _trayStrip.anchoredPosition = Vector2.zero;
            _trayStrip.sizeDelta = new Vector2(0f, 410f);


            _attractorSlot = BuildSlot("AttractorSlot", ShapeSprites.Ring, new Vector2(0f, 0f), new Vector2(UITokens.SpaceM, 232f), out _attractorCount);
            _repulsorSlot = BuildSlot("RepulsorSlot", ShapeSprites.BrokenRing, new Vector2(0f, 0f), new Vector2(UITokens.SpaceM + SlotSize + 20f, 232f), out _repulsorCount);

            _undo = UIFactory.CreateRoundButton(Content, "Undo", UIIcons.Get("undo"), RoundButton, style, () => onUndo());
            UIFactory.Place((RectTransform)_undo.transform, new Vector2(1f, 0f), new Vector2(-UITokens.SpaceM - RoundButton - 20f, 252f), new Vector2(RoundButton, RoundButton));
            _restart = UIFactory.CreateRoundButton(Content, "Restart", UIIcons.Get("restart"), RoundButton, style, () => onRestart());
            UIFactory.Place((RectTransform)_restart.transform, new Vector2(1f, 0f), new Vector2(-UITokens.SpaceM, 252f), new Vector2(RoundButton, RoundButton));

            _launch = UIFactory.CreateButton(
                Content, "Launch", "Launch", new Vector2(560f, 128f), style, () => onLaunch(), primary: true, fontSize: 56f,
                icon: UIIcons.Get("play"));
            UIFactory.Place((RectTransform)_launch.transform, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(560f, 128f));

            // What a hint costs, on the corner of its button: the coins, or "AD" when there are not enough.
            Image chip = UIFactory.CreateBadge(Content, "HintPrice", new Vector2(112f, 50f), style, out _hintPrice);
            UIFactory.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(-UITokens.SpaceM + 6f, -UITokens.SpaceM - RoundButton + 26f), new Vector2(112f, 50f));

            // Offered after a run of misses: one more well for a rewarded ad.
            _extraWell = UIFactory.CreateButton(
                Content, "ExtraWell", Strings.Game.ExtraWell, new Vector2(350f, 108f), style, () => ExtraWellPressed?.Invoke(),
                fontSize: 40f, icon: UIIcons.Get("shop"));
            UIFactory.Place((RectTransform)_extraWell.transform, new Vector2(0.5f, 0f), new Vector2(40f, 244f), new Vector2(350f, 108f));
            Image adChip = UIFactory.CreateBadge(_extraWell.transform, "AdChip", new Vector2(76f, 40f), style, out TextMeshProUGUI adLabel);
            adLabel.text = "AD";
            UIFactory.Place(adChip.rectTransform, new Vector2(1f, 1f), new Vector2(10f, 14f), new Vector2(76f, 40f));
            _extraWell.gameObject.SetActive(false);

            // The lesson line of the first levels, under the counters: one sentence, never in the way of a touch.
            Image banner = UIFactory.CreateBadge(Content, "Lesson", new Vector2(980f, 128f), style, out _lessonText);
            _lessonText.fontSize = 38f;
            _lessonText.enableWordWrapping = true;
            UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -246f - UITokens.SpaceM), new Vector2(980f, 128f));
            _lesson = banner.gameObject;
            _lesson.SetActive(false);

            // The ghost finger of the level-1 lesson, above everything else on the screen.
            RectTransform hand = UIFactory.CreateRect(Root.transform, "TutorialHand");
            UIFactory.Place(hand, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HandSize, HandSize));
            hand.pivot = new Vector2(0.5f, 0.5f);
            _handRing = UIFactory.CreateIconImage(hand, "Ring", ShapeSprites.Ring, HandSize);
            UIFactory.Stretch(_handRing.rectTransform);
            _handDot = UIFactory.CreateIconImage(hand, "Dot", ShapeSprites.Disc, HandSize * 0.5f);
            UIFactory.Place(_handDot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HandSize * 0.5f, HandSize * 0.5f));
            _handDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _handRing.raycastTarget = false;
            _handDot.raycastTarget = false;
            _hand = hand;
            _hand.gameObject.SetActive(false);
        }

        private TextMeshProUGUI _lessonText;
        private GameObject _lesson;

        public bool LessonVisible => _lesson.activeSelf;

        public void SetLesson(string text)
        {
            bool any = !string.IsNullOrEmpty(text);
            if (any)
            {
                _lessonText.text = text;
            }
            _lesson.SetActive(any);
        }

        private const float HandSize = 150f;
        private readonly TextMeshProUGUI _hintPrice;
        private readonly Button _extraWell;
        private readonly RectTransform _hand;
        private readonly Image _handRing;
        private readonly Image _handDot;

        // Set by GameManager: the Extra Well button was pressed.
        public Action ExtraWellPressed { get; set; }

        // "30" (coins) when a hint can be paid for, "AD" when it would take a rewarded ad.
        public void SetHintPrice(bool canAfford)
        {
            _hintPrice.text = canAfford ? Economy.HintCost.ToString() : "AD";
        }

        public void SetExtraWellOffer(bool visible)
        {
            if (_extraWell.gameObject.activeSelf != visible)
            {
                _extraWell.gameObject.SetActive(visible);
            }
        }

        // Where the attractor slot is on the screen, for the lesson's finger to start from.
        public Vector2 TrayScreenCenter()
        {
            return RectTransformUtility.WorldToScreenPoint(null, _attractorSlot.TransformPoint(_attractorSlot.rect.center));
        }

        // The lesson's ghost finger, at a point on the screen. pressed = it is holding a well (it draws smaller, as a
        // finger does when it presses).
        public void ShowHand(Vector2 screenPoint, float alpha, bool pressed)
        {
            if (alpha <= 0.001f)
            {
                _hand.gameObject.SetActive(false);
                return;
            }
            _hand.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Root.transform, screenPoint, null, out Vector2 local);
            _hand.anchoredPosition = local;
            _hand.localScale = Vector3.one * (pressed ? 0.8f : 1f);
            Color ink = _style.Palette.Text;
            _handRing.color = new Color(ink.r, ink.g, ink.b, 0.55f * alpha);
            _handDot.color = new Color(ink.r, ink.g, ink.b, 0.35f * alpha);
        }

        public void HideHand() => _hand.gameObject.SetActive(false);

        public RectTransform AttractorSlot => _attractorSlot;

        // The hint button swells once: after a few misses, a nudge toward the help that is there.
        private float _hintPulseAge = -1f;
        private const float HintPulseSeconds = 0.8f;

        public void PulseHint() => _hintPulseAge = 0f;

        // Call every frame, with unscaled time (the HUD is live while the game is paused).
        public void Tick(float unscaledSeconds)
        {
            if (_hintPulseAge < 0f)
            {
                return;
            }
            _hintPulseAge += unscaledSeconds;
            float scale = 1f + 0.32f * FeelCurves.Bump(_hintPulseAge, HintPulseSeconds);
            _hint.transform.localScale = new Vector3(scale, scale, 1f);
            if (_hintPulseAge >= HintPulseSeconds)
            {
                _hintPulseAge = -1f;
                _hint.transform.localScale = Vector3.one;
            }
        }

        // Set by GameManager; only ever called in a development build.
        public Action SecretTapped { get; set; }

        private int _secretTaps;
        private float _lastSecretTap;

        private void CountSecretTap()
        {
            float now = Time.realtimeSinceStartup;
            _secretTaps = now - _lastSecretTap < 1.2f ? _secretTaps + 1 : 1;
            _lastSecretTap = now;
            if (_secretTaps >= 7)
            {
                _secretTaps = 0;
                SecretTapped?.Invoke();
            }
        }

        // Level title and the counters ----------------------------------------------------

        public void ShowLevel(int level, bool daily) => _level.text = daily ? "Daily" : Strings.Game.Level(level);

        public void SetGates(int lit, int total)
        {
            if (lit == _litShown && total == _gatesShown)
            {
                return;
            }
            _litShown = lit;
            _gatesShown = total;
            _gates.text = "Gates " + lit + "/" + total;
        }

        public void SetWells(int used, int par)
        {
            if (used == _usedShown && par == _parShown)
            {
                return;
            }
            _usedShown = used;
            _parShown = par;
            _wells.text = "Wells " + used + " (par " + par + ")";
        }

        public void SetTray(int attractorsLeft, int repulsorsLeft, bool showRepulsors)
        {
            _attractorCount.text = attractorsLeft.ToString();
            _attractorSlot.GetComponent<CanvasGroup>().alpha = attractorsLeft > 0 ? 1f : 0.35f;

            _repulsorSlot.gameObject.SetActive(showRepulsors);
            _repulsorCount.text = repulsorsLeft.ToString();
            _repulsorSlot.GetComponent<CanvasGroup>().alpha = repulsorsLeft > 0 ? 1f : 0.35f;
        }

        public void SetUndoAvailable(bool available)
        {
            _undo.interactable = available;
        }

        // While the ship flies, the controls are shown but cannot be pressed.
        public void SetControlsEnabled(bool enabled)
        {
            _launch.interactable = enabled;
            _restart.interactable = enabled;
            _hint.interactable = enabled;
        }

        // IWellTray ------------------------------------------------------------------------

        public bool TryPickSlot(Vector2 screenPoint, out WellKind kind)
        {
            if (Contains(_attractorSlot, screenPoint))
            {
                kind = WellKind.Attractor;
                return true;
            }
            if (_repulsorSlot.gameObject.activeSelf && Contains(_repulsorSlot, screenPoint))
            {
                kind = WellKind.Repulsor;
                return true;
            }
            kind = WellKind.Attractor;
            return false;
        }

        public bool IsOverTray(Vector2 screenPoint) => Contains(_trayStrip, screenPoint);

        public override void OnBack() => _onBack();

        // ----------------------------------------------------------------------------------

        private static bool Contains(RectTransform rect, Vector2 screenPoint)
        {
            // The canvas is Screen Space Overlay, so the screen point is the canvas point and no camera is needed.
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
        }

        // A tray slot: a card with the well's outline on it and how many are left. Not a Button: the
        // drag controller reads presses on it itself, so the press must not be eaten by the UI.
        private RectTransform BuildSlot(string name, Sprite glyph, Vector2 anchor, Vector2 position, out TextMeshProUGUI count)
        {
            Image card = UIFactory.CreateSurface(Content, name, _style);
            card.raycastTarget = false;
            UIFactory.Place(card.rectTransform, anchor, position, new Vector2(SlotSize, SlotSize));
            card.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            Image icon = UIFactory.CreateIconImage(card.rectTransform, "Glyph", glyph, 72f);
            icon.raycastTarget = false;
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(72f, 72f));
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _style.RegisterIcon(icon);

            count = UIFactory.CreateText(card.rectTransform, "Count", string.Empty, 44f, TextAlignmentOptions.Center, _style, bold: true);
            UIFactory.Place(count.rectTransform, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(SlotSize, 56f));
            count.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return card.rectTransform;
        }
    }
}
