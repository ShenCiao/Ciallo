using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ciallo.Geometry;
using Ciallo.Tool;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

/// <summary>
/// Responsible for collecting and dispatching canvas gui input events.
/// Current version also handles canvas navigation. May change in the future.
/// </summary>
public partial class WorldEventDispatcher : Container
{
    private Camera2D _camera;
    private bool _isPanning;

    private Vector2 _prevScreenPos;
    private Vector2 _prevWorldPos;
    private float _prevPressure;
    private Vector2 _prevTilt;

    private Stopwatch _timer;

    public Entity Document;
    private ToolManager ToolManager => Document.Get<ToolManager>();

    public CursorMotionData CurrentCursorMotion { get => throw new NotImplementedException(); internal set; }


    // ------------ Touch gesture state -------------
    private readonly Dictionary<int, Vector2> _activeTouches = new();
    private bool _isTouchDragging;
    private int _maxTouchCount;

    public override void _Ready()
    {
        _timer = Stopwatch.StartNew();

        _camera = GetNode<Camera2D>("%MainCamera");

        GuiInput += OnGuiInput;
    }

    public void OnGuiInput(InputEvent e)
    {
        if (!Document.IsAlive) return; // This check prevents errors when the document is closed.
        // The container is queued for deletion but the Document entity is freed immediately, which can cause this method to be called on a disposed entity.

        if (HandleTouchGesture(e)) return;
        // Suppress emulated mouse events while 2+ fingers are active.
        if (Input.EmulateMouseFromTouch && _activeTouches.Count >= 2) return;
        if (HandleKeyEvent(e)) return;
        if (e is InputEventMouse mouseEvent)
            HandleMouseEvent(mouseEvent);
    }

    private bool HandleKeyEvent(InputEvent e)
    {
        if (e is not InputEventKey key) return false;

        DispatchKey(key);
        return true;
    }

    private void HandleMouseEvent(InputEventMouse mouseEvent)
    {
        // Godot treats stylus pen input as mouse input.
        var cursor = GetMouseCursorState(mouseEvent);
        var handled = false;

        if (mouseEvent is InputEventMouseButton mouseButton && !_isPanning)
        {
            handled = DispatchMouseButton(mouseButton, new()
            {
                ScreenPosition = cursor.ScreenPosition,
                WorldPosition = cursor.WorldPosition,
                Tilt = _prevTilt,
            });
        }
        if (!handled)
            handled = HandleCanvasNavigation(mouseEvent, (PaintPanel)Owner, cursor);

        DispatchMouseMotion(mouseEvent, cursor);
        UpdateMouseCursorState(cursor);

        if (handled)
            GetViewport().SetInputAsHandled();
    }

    private MouseCursorState GetMouseCursorState(InputEventMouse mouseEvent)
    {
        // Pitfall _camera.GetViewportTransform() doesn't update until the end of frame, so not response to property change.
        var panel = (PaintPanel)Owner;
        var screenPos = mouseEvent.Position;
        var screenDelta = screenPos - _prevScreenPos;
        var invTransform = _camera.GetViewportTransform().AffineInverse();
        var cameraWorldPos = invTransform * screenPos;
        var prevCameraWorldPosWithCurrentCamera = invTransform * _prevScreenPos;
        var worldPos = panel.ToDocumentPosition(cameraWorldPos);
        var prevWorldPosWithCurrentCamera = panel.ToDocumentPosition(prevCameraWorldPosWithCurrentCamera);
        var cameraWorldDeltaBeforeTransformCamera = cameraWorldPos - prevCameraWorldPosWithCurrentCamera;
        var worldDelta = worldPos - _prevWorldPos;

        return new MouseCursorState(screenPos, screenDelta, worldPos, worldDelta, cameraWorldDeltaBeforeTransformCamera);
    }

    private bool HandleCanvasNavigation(InputEventMouse mouseEvent, PaintPanel panel, MouseCursorState cursor)
    {
        var handled = _isPanning;

        if (mouseEvent is InputEventMouseMotion && _isPanning)
            panel.CameraOffset.Value -= cursor.WorldDeltaBeforeTransformCamera;

        // Drag middle mouse to pan
        if (mouseEvent is InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: true })
        {
            _isPanning = true;
            handled = true;
        }
        if (mouseEvent is InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: false })
        {
            _isPanning = false;
            handled = true;
        }

        // Double click to reset camera.
        if (mouseEvent is InputEventMouseButton { ButtonIndex: MouseButton.Middle, DoubleClick: true })
        {
            panel.CameraOffset.Value = Vector2.Zero;
            panel.CameraZoom.Value = 1.0f;
            panel.CameraRotation.Value = 0.0f;
            panel.MirrorHorizontal.Value = false;
            panel.MirrorVertical.Value = false;
            handled = true;
        }

        // Scroll mouse wheel to zoom camera.
        if (mouseEvent is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, AltPressed: false })
        {
            panel.CameraZoom.Value *= 1.0f + AppPreference.MouseWheelZoomFactor.Value;
            handled = true;
        }
        else if (mouseEvent is InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, AltPressed: false })
        {
            panel.CameraZoom.Value *= 1.0f - AppPreference.MouseWheelZoomFactor.Value;
            handled = true;
        }

        // Alt + scroll mouse wheel to rotate camera.
        if (mouseEvent is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, AltPressed: true })
        {
            panel.CameraRotation.Value += AppPreference.MouseWheelRotateFactor.Value;
            handled = true;
        }
        else if (mouseEvent is InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, AltPressed: true })
        {
            panel.CameraRotation.Value -= AppPreference.MouseWheelRotateFactor.Value;
            handled = true;
        }

        return handled;
    }

    private void DispatchMouseMotion(InputEventMouse mouseEvent, MouseCursorState cursor)
    {
        if (!cursor.WorldDelta.IsZeroApprox()) // dispatch motion
        {
            if (mouseEvent is InputEventMouseMotion motion)
            {
                var currentPressure = AppPreference.PenPressureRemapCurve.Value.SampleX(motion.Pressure);

                DispatchMotion(new()
                {
                    ScreenPosition = cursor.ScreenPosition,
                    ScreenDelta = cursor.ScreenDelta,
                    WorldPosition = cursor.WorldPosition,
                    WorldDelta = cursor.WorldDelta,
                    Pressure = currentPressure,
                    PressureDelta = currentPressure - _prevPressure,
                    Tilt = motion.Tilt,
                    TiltDelta = motion.Tilt - _prevTilt,
                    TimeDelta = _timer.Elapsed,
                });

                _prevPressure = currentPressure;
                _prevTilt = motion.Tilt;
            }
            else
            {
                // Motion without InputEventMouseMotion, e.g. mouse wheel zoom causing world position change.
                // dispatch with previous pressure and tilt.
                DispatchMotion(new()
                {
                    ScreenPosition = cursor.ScreenPosition,
                    ScreenDelta = cursor.ScreenDelta,
                    WorldPosition = cursor.WorldPosition,
                    WorldDelta = cursor.WorldDelta,
                    Pressure = _prevPressure,
                    PressureDelta = 0,
                    Tilt = _prevTilt,
                    TiltDelta = Vector2.Zero,
                    TimeDelta = _timer.Elapsed,
                });
            }
        }
    }

    private void UpdateMouseCursorState(MouseCursorState cursor)
    {
        _timer.Restart();
        _prevScreenPos = cursor.ScreenPosition;
        _prevWorldPos = cursor.WorldPosition;
    }

    private readonly struct MouseCursorState
    {
        public readonly Vector2 ScreenPosition;
        public readonly Vector2 ScreenDelta;
        public readonly Vector2 WorldPosition;
        public readonly Vector2 WorldDelta;
        public readonly Vector2 WorldDeltaBeforeTransformCamera;

        public MouseCursorState(
            Vector2 screenPosition,
            Vector2 screenDelta,
            Vector2 worldPosition,
            Vector2 worldDelta,
            Vector2 worldDeltaBeforeTransformCamera)
        {
            ScreenPosition = screenPosition;
            ScreenDelta = screenDelta;
            WorldPosition = worldPosition;
            WorldDelta = worldDelta;
            WorldDeltaBeforeTransformCamera = worldDeltaBeforeTransformCamera;
        }
    }

    /// <summary>
    /// Handles InputEventScreenTouch and InputEventScreenDrag for multi-touch gestures.
    /// Returns true when the event is consumed and mouse processing should be skipped.
    /// Gesture rules:
    ///   - 2-finger tap (no drag) → Undo
    ///   - 3-finger tap (no drag) → Redo
    ///   - 2-finger drag (never exceeded 2 fingers) → pan + pinch-zoom around centroid
    ///   - Adding a 3rd finger mid-drag cancels pan/zoom; all gestures ignored until all fingers lift.
    /// </summary>
    private bool HandleTouchGesture(InputEvent e)
    {
        if (e is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                _activeTouches[touch.Index] = touch.Position;
                if (_activeTouches.Count > _maxTouchCount)
                    _maxTouchCount = _activeTouches.Count;
            }
            else
            {
                // Trigger tap gesture on last finger release
                if (_activeTouches.Count == 1 && !_isTouchDragging)
                {
                    var cmdM = Document.Get<CommandManager>();
                    switch (_maxTouchCount)
                    {
                        case 2: cmdM.Undo(); break;
                        case 3: cmdM.Redo(); break;
                    }
                }
                _activeTouches.Remove(touch.Index);
                if (_activeTouches.Count == 0)
                {
                    _maxTouchCount = 0;
                    _isTouchDragging = false;
                }
            }
            return true;
        }

        if (e is InputEventScreenDrag drag)
        {
            _isTouchDragging = true;
            // Only do pan/zoom for exactly 2 fingers that never exceeded 2.
            if (_activeTouches.Count == 2 && _maxTouchCount == 2)
            {
                // Find the stationary finger's position.
                Vector2 otherPos = Vector2.Zero;
                foreach (var (idx, pos) in _activeTouches)
                    if (idx != drag.Index)
                        otherPos = pos;

                var prevThisPos = _activeTouches[drag.Index];
                var prevCentroid = (prevThisPos + otherPos) * 0.5f;
                var prevVector = prevThisPos - otherPos;
                var prevDist = prevThisPos.DistanceTo(otherPos);

                _activeTouches[drag.Index] = drag.Position;

                var newCentroid = (drag.Position + otherPos) * 0.5f;
                var newVector = drag.Position - otherPos;
                var newDist = drag.Position.DistanceTo(otherPos);

                // Pitfall: same as mouse pan — GetViewportTransform() is stale until end of frame.
                var invT = _camera.GetViewportTransform().AffineInverse();
                var panel = (PaintPanel)Owner;
                var oldOffset = panel.CameraOffset.Value;
                var oldZoom = panel.CameraZoom.Value;
                var oldRotation = panel.CameraRotation.Value;
                var worldUnderPrevCentroid = invT * prevCentroid;

                var newZoom = oldZoom;
                var newRotation = oldRotation;
                if (prevDist > 1f && newDist > 1f)
                {
                    newZoom *= newDist / prevDist;
                    newRotation -= newVector.Angle() - prevVector.Angle();
                }

                var viewportCenter = prevCentroid -
                                     (worldUnderPrevCentroid - oldOffset).Rotated(-oldRotation) * oldZoom;
                var newOffset = worldUnderPrevCentroid -
                                (newCentroid - viewportCenter).Rotated(newRotation) / newZoom;

                panel.CameraOffset.Value = newOffset;
                panel.CameraZoom.Value = newZoom;
                panel.CameraRotation.Value = newRotation;
            }
            else
            {
                _activeTouches[drag.Index] = drag.Position;
            }
            return true;
        }

        return false;
    }

    private void DispatchKey(InputEventKey key)
    {
        if (ToolManager.WorkingTool.CurrentValue?.OnKey(key) == true)
            GetViewport().SetInputAsHandled();
    }

    private bool DispatchMouseButton(InputEventMouseButton mouse, CursorButtonData data)
    {
        return ToolManager.WorkingTool.CurrentValue?.OnMouseButton(mouse, data) == true;
    }

    public void DispatchMotion(CursorMotionData data)
    {
        ToolManager.WorkingTool.CurrentValue?.OnMoving(data);
    }
}
