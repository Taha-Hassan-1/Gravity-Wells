using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GravityWells.UI
{
    // A small squash while a button is held, plus the UI tap sound, so taps feel acknowledged
    // before the action lands. It resets on release, when the finger slides off, and on disable.
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.95f;

        private Selectable _selectable;
        private UIStyle _style;

        public void Init(Selectable selectable, UIStyle style)
        {
            _selectable = selectable;
            _style = style;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // A disabled button gets pointer events too; it should neither squash nor click.
            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }
            transform.localScale = Vector3.one * PressedScale;
            _style?.ButtonPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;

        public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;

        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
