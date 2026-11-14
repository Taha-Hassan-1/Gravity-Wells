using System;
using TMPro;
using UnityEngine;
using GravityWells.Core;
using UnityEngine.UI;

namespace GravityWells.UI
{
    // Full-screen modal for the moments around an ad: a spinner while one loads or opens, and a
    // message with an OK button when none can be shown. Its dim backdrop eats every tap, and
    // GameManager cuts board input separately, so nothing underneath can be touched meanwhile.
    public sealed class AdOverlay
    {
        private const int DotCount = 8;
        private const float DotRadius = 70f;
        private const float DotSize = 26f;
        private const float SpinSecondsPerTurn = 0.9f;
        // Sits between the spinner (bottom edge about -13) and the button (top edge -230).
        private const float LoadingBodyY = -120f;

        private readonly GameObject _root;
        private readonly UIStyle _style;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _body;
        private readonly RectTransform _spinner;
        private readonly Image[] _dots = new Image[DotCount];
        private readonly Button _button;
        private readonly TMP_Text _buttonLabel;

        private float _spin;

        public AdOverlay(Transform canvas, UIStyle style, Action onButton)
        {
            _style = style;

            RectTransform panel = UIFactory.CreateRect(canvas, "AdOverlay");
            UIFactory.Stretch(panel);
            _root = panel.gameObject;

            Image dim = UIFactory.CreateFlat(panel, "Dim", new Color(0f, 0f, 0f, 0.78f));
            UIFactory.Stretch(dim.rectTransform);

            Image card = UIFactory.CreateCard(panel, "Card", style);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 820f));

            _title = UIFactory.CreateText(
                card.rectTransform, "Title", string.Empty, 70f, TextAlignmentOptions.Center, style, bold: true);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(780f, 100f));

            _spinner = UIFactory.CreateRect(card.rectTransform, "Spinner");
            UIFactory.Place(_spinner, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(DotRadius * 2f, DotRadius * 2f));
            for (int i = 0; i < DotCount; i++)
            {
                _dots[i] = UIFactory.CreateFlat(_spinner, "Dot" + i, Color.white);
                float angle = i / (float)DotCount * Mathf.PI * 2f;
                UIFactory.Place(
                    _dots[i].rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * DotRadius, new Vector2(DotSize, DotSize));
                _dots[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            _body = UIFactory.CreateText(
                card.rectTransform, "Body", string.Empty, 40f, TextAlignmentOptions.Center, style, dim: true);
            UIFactory.Place(_body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, LoadingBodyY), new Vector2(740f, 200f));
            _body.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var size = new Vector2(520f, 120f);
            _button = UIFactory.CreateButton(card.rectTransform, "Button", Strings.Common.Ok, size, style, () => onButton?.Invoke(), primary: true, fontSize: 46f);
            UIFactory.Place((RectTransform)_button.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), size);
            _buttonLabel = UIFactory.LabelOf(_button);

            _root.SetActive(false);
        }

        public bool Visible => _root.activeSelf;

        // Cancellable while waiting for a load; not once the ad itself is opening.
        public void ShowLoading(string title, string body, bool cancellable)
        {
            Present(title, body, spinning: true);
            _button.gameObject.SetActive(cancellable);
            _buttonLabel.text = Strings.Common.Cancel;
        }

        public void ShowMessage(string title, string body)
        {
            Present(title, body, spinning: false);
            _button.gameObject.SetActive(true);
            _buttonLabel.text = Strings.Common.Ok;
        }

        public void Hide() => _root.SetActive(false);

        private void Present(string title, string body, bool spinning)
        {
            _title.text = title;
            _body.text = body;
            _spinner.gameObject.SetActive(spinning);
            // A message has no spinner, so give the text the room it left.
            _body.rectTransform.anchoredPosition = new Vector2(0f, spinning ? LoadingBodyY : 20f);
            _spin = 0f;
            _root.SetActive(true);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_root.activeSelf || !_spinner.gameObject.activeSelf)
            {
                return;
            }

            _spin = (_spin + unscaledDeltaTime / SpinSecondsPerTurn) % 1f;
            Color color = _style.Palette.Accent;
            for (int i = 0; i < DotCount; i++)
            {
                // A fading tail that chases the head dot around the ring.
                float behind = Mathf.Repeat(_spin - i / (float)DotCount, 1f);
                color.a = Mathf.Lerp(1f, 0.15f, behind);
                _dots[i].color = color;
            }
        }
    }
}
