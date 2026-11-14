using UnityEngine;
using GravityWells.Core;
using GravityWells.UI;

namespace GravityWells.Game
{
    // Keeps the orthographic camera framing the field: centred in the space left once the HUD above and the
    // tray below are taken out, and inside the device safe area (notch, gesture bar). The maths is CameraFit
    // (tested for 4:3 through 21:9); this only feeds it the screen and reapplies when the screen changes.
    public sealed class CameraFitter : MonoBehaviour
    {
        private Camera _camera;
        private Vector2Int _lastSize;
        private Rect _lastSafeArea;
        private float _z;

        public void Init(Camera camera)
        {
            _camera = camera;
            _z = camera.transform.position.z;
            Apply(force: true);
        }

        private void LateUpdate() => Apply(force: false);

        private void Apply(bool force)
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            if (!force && size == _lastSize && safe == _lastSafeArea)
            {
                return;
            }
            _lastSize = size;
            _lastSafeArea = safe;

            float aspect = (float)size.x / Mathf.Max(1, size.y);
            UIRoot.ComputeSafeAnchors(safe, size, out Vector2 min, out Vector2 max);
            CameraFitResult fit = CameraFit.ComputeForField(aspect, extraTopReserve: 1f - max.y, extraBottomReserve: min.y);

            _camera.orthographicSize = fit.OrthographicSize;
            _camera.transform.position = new Vector3(fit.Center.x, fit.Center.y, _z);
        }
    }
}
