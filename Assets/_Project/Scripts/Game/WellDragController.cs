using System;
using UnityEngine;
using GravityWells.Core;
using GravityWells.Services;

namespace GravityWells.Game
{
    // What the controller needs from the screen's tray: which slot a screen point is on, and whether a point
    // is somewhere letting go of a well removes it. The HUD implements it; a test can fake it.
    public interface IWellTray
    {
        bool TryPickSlot(Vector2 screenPoint, out WellKind kind);
        bool IsOverTray(Vector2 screenPoint);
    }

    // Turns a finger into well edits. Legacy Input Manager only (Input.touchCount / Input.GetMouseButton),
    // one pointer: a drag belongs to the finger that started it, and a second finger neither takes it over
    // nor starts anything.
    //
    //   - press on a tray slot: take a well off the tray, if one is left
    //   - press on a placed well (within the touch radius): pick it up
    //   - drag: the well follows the finger and the preview is re-flown every frame it moved
    //   - lift over the tray or off the field: the well is removed; anywhere else it is placed
    //
    // It allocates nothing while dragging.
    public sealed class WellDragController
    {
        private const int MouseFinger = -1;    // real fingers have ids >= 0

        private readonly Camera _camera;
        private readonly WellLayout _layout;
        private readonly IWellTray _tray;
        private readonly HapticService _haptics;
        private readonly Action<bool> _layoutChanged;   // true when the change is settled (a drop, not a step of a drag)
        private readonly Action _noneLeft;
        private readonly Action _pickedUp;
        private readonly Action<int> _dropped;

        private int _activeFinger = MouseFinger;
        private bool _enabled;
        private Vector2 _lastWorld;
        private bool _lastRemoveZone;

        public WellDragController(
            Camera camera, WellLayout layout, IWellTray tray, HapticService haptics, Action<bool> layoutChanged, Action noneLeft,
            Action pickedUp, Action<int> dropped)
        {
            _camera = camera;
            _layout = layout;
            _tray = tray;
            _haptics = haptics;
            _layoutChanged = layoutChanged;
            _pickedUp = pickedUp;
            _dropped = dropped;
            _noneLeft = noneLeft;
        }

        public bool IsDragging => _layout.IsDragging;

        // How far from a placed well a press still grabs it, in world units.
        public float TouchRadiusWorldUnits => ComputeTouchRadiusWorldUnits(
            GameConfig.TouchRadiusDp, Screen.dpi, _camera.orthographicSize, Screen.height, GameConfig.MinTouchRadiusWorldUnits);

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled && _layout.IsDragging)
            {
                _layout.CancelDrag(); // never leave a well stuck in a finger when input is cut off
                _activeFinger = MouseFinger;
                _layoutChanged(true);
            }
        }

        public void Tick()
        {
            if (!_enabled)
            {
                return;
            }

            if (_layout.IsDragging && _activeFinger != MouseFinger)
            {
                FollowActiveFinger();
                return;
            }
            if (Input.touchCount > 1 && !_layout.IsDragging)
            {
                return; // two fingers (a pinch, a palm) start nothing
            }

            Vector2 screen;
            bool down, held, up;
            int finger = MouseFinger;
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                finger = touch.fingerId;
                screen = touch.position;
                down = touch.phase == TouchPhase.Began;
                held = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            }
            else
            {
                screen = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
            }

            if (down && !_layout.IsDragging)
            {
                Begin(screen, finger);
            }
            else if (_layout.IsDragging && up)
            {
                End();
            }
            else if (_layout.IsDragging && held)
            {
                Move(screen);
            }
        }

        private void FollowActiveFinger()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != _activeFinger)
                {
                    continue;
                }
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    End();
                }
                else
                {
                    Move(touch.position);
                }
                return;
            }
            End(); // its finger is gone without an Ended event (an interruption, a gesture-navigation swipe)
        }

        private void Begin(Vector2 screen, int finger)
        {
            Vector2 world = ToWorld(screen);

            if (_tray.TryPickSlot(screen, out WellKind kind))
            {
                if (!_layout.BeginDragNew(kind, world))
                {
                    _noneLeft();
                    return;
                }
            }
            else
            {
                int index = _layout.NearestWell(world, TouchRadiusWorldUnits);
                if (index < 0 || !_layout.BeginDragExisting(index))
                {
                    return;
                }
            }

            _activeFinger = finger;
            _haptics.Tap();
            _pickedUp();
            Move(screen, force: true);
        }

        private void Move(Vector2 screen, bool force = false)
        {
            Vector2 world = ToWorld(screen);
            bool removeZone = _tray.IsOverTray(screen) || !IsOnField(world);
            if (!force && world == _lastWorld && removeZone == _lastRemoveZone)
            {
                return; // the preview is only re-flown when the finger actually moved
            }
            _lastWorld = world;
            _lastRemoveZone = removeZone;

            _layout.DragTo(new Vector2(world.x, world.y + GameConfig.DragLiftWorldUnits), removeZone);
            _layoutChanged(false);
        }

        private void End()
        {
            int index = _layout.DraggedIndex;
            bool removed = _layout.DragWillRemove;
            _layout.EndDrag();
            _activeFinger = MouseFinger;
            _layoutChanged(true);
            _haptics.Tap();
            _dropped(removed ? -1 : index);
        }

        private Vector2 ToWorld(Vector2 screen)
        {
            Vector3 p = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            return new Vector2(p.x, p.y);
        }

        private static bool IsOnField(Vector2 world)
        {
            return world.x >= 0f && world.x <= GameConfig.FieldWidth && world.y >= 0f && world.y <= GameConfig.FieldHeight;
        }

        public static float ComputeTouchRadiusWorldUnits(
            float dp, float screenDpi, float orthographicSize, float screenHeightPx, float minWorldUnits)
        {
            float dpi = screenDpi > 0f ? screenDpi : 160f; // the Editor and some devices report 0
            float pixels = dp * dpi / 160f;
            float worldUnitsPerPixel = orthographicSize * 2f / Mathf.Max(1f, screenHeightPx);
            return Mathf.Max(minWorldUnits, pixels * worldUnitsPerPixel);
        }
    }
}
