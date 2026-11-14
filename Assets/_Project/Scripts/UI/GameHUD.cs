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
        }

        public RectTransform AttractorSlot => _attractorSlot;

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

        public void ShowLevel(int level) => _level.text = "Level " + level;

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
