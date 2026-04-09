/* This file is originally copied from Godot 4.4 curve_editor_plugin.cpp, translated to C# with AI tool, proof-edited and modified by human.
Shen: this control took me around two hours to make it work correctly at basic level.
Shen: Bezier curve took me two and this control took me one entire working day to further modify.
Sep 15 2025: Given the complexity of this control, Shen still consider this as a productive workflow.
*/

//Pitfall: Godot C++ constructs `Transform2D` default value is Transform2D.Identity, but in C# it is zero.

using System.Collections.Generic;
using System.Linq;
using Ciallo.Geometry;
using Godot;

namespace Ciallo.Widget;

/// <summary>
/// The godot editor's curve edit control at runtime.
/// </summary>
[Tool, GlobalClass]
public partial class MappingCurveEdit : Control
{
    [Export] public float MinDomain = 0.0f;
    [Export] public float MaxDomain = 1.0f;
    [Export] public float MinValue = 0.0f;
    [Export] public float MaxValue = 1.0f;
    public float DomainRange => MaxDomain - MinDomain;
    public float ValueRange => MaxValue - MinValue;

    private const float AspectRatio = 1.0f;
    private const float LineWidth = 0.5f;
    private const int StepSize = 2; // Number of pixels between plot points.

    private BezierCurve _curve;
    private Transform2D _worldToView;
    private int _selectedIndex = -1;
    private int _hoveredIndex = -1;
    private TangentIndex _selectedTangentIndex = TangentIndex.None;
    private TangentIndex _hoveredTangentIndex = TangentIndex.None;

    private int _pointRadius = 4; // in pixel
    private int _hoverRadius = 10;
    private int _tangentRadius = 3;
    private int _tangentHoverRadius = 8;

    private GrabMode _grabbing = GrabMode.None;
    private Vector2 _initialGrabPos;
    private int _initialGrabIndex = -1;
    private BezierCurve.HandleControlMode _initialHandleMode = BezierCurve.HandleControlMode.LinearEqual;

    public enum PresetId
    {
        Constant = 0,
        Linear,
        EaseIn,
        EaseOut,
        Smoothstep,
        Count
    }

    public enum TangentIndex
    {
        None = -1,
        Left = 0,
        Right = 1
    }

    private enum GrabMode
    {
        None,
        Add,
        Move
    }

    public MappingCurveEdit()
    {
        FocusMode = FocusModeEnum.All;
        ClipContents = true;
    }

    public BezierCurve Curve
    {
        get => _curve;
        set
        {
            if (_curve == value) return;
            if (value == null) return;
            if (!value.IsXMonotone)
            {
                GD.PushWarning("The provided curve is not X-monotone. Assignment aborted.");
                return;
            }
            _curve = value;
            _CurveChanged();
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        return new Vector2(256, 256);
    }

    public void UsePreset(PresetId presetId)
    {
        if (_curve == null || presetId < 0 || presetId >= PresetId.Count)
            return;

        var preset = presetId switch
        {
            PresetId.Constant => BezierCurve.Constant((MinValue + MaxValue) / 2f),
            PresetId.Linear => BezierCurve.Linear(MinValue, MaxValue),
            PresetId.EaseIn => BezierCurve.EaseIn(MinValue, MaxValue),
            PresetId.EaseOut => BezierCurve.EaseOut(MinValue, MaxValue),
            PresetId.Smoothstep => BezierCurve.EaseInOut(MinValue, MaxValue),
            _ => null
        };
        if (preset == null) return;

        // Modify the existing curve in-place so that external references remain valid.
        _curve.Points = preset.Points;
        SetSelectedIndex(-1);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_curve == null)
            return;

        if (@event is InputEventKey { Pressed: true } keyEvent)
        {
            if (keyEvent.Keycode == Key.Delete)
            {
                if (_selectedTangentIndex != TangentIndex.None)
                {
                    ResetLinear(_selectedIndex, _selectedTangentIndex);
                }
                else if (_selectedIndex != -1)
                {
                    if (_grabbing == GrabMode.Add)
                    {
                        _curve.RemovePoint(_selectedIndex);
                        SetSelectedIndex(-1);
                    }
                    else
                    {
                        RemovePoint(_selectedIndex);
                    }
                    _grabbing = GrabMode.None;
                    _hoveredIndex = -1;
                    _hoveredTangentIndex = TangentIndex.None;
                }
                AcceptEvent();
            }

            if (keyEvent.Keycode == Key.Shift || keyEvent.Keycode == Key.Alt)
            {
                QueueRedraw();
            }
        }

        if (@event is InputEventMouseButton { Pressed: true } mouseButton)
        {
            Vector2 mpos = mouseButton.Position;

            if (mouseButton.ButtonIndex is MouseButton.Right or MouseButton.Middle)
            {
                // Cancel any ongoing grab operation.
                if (mouseButton.ButtonIndex is MouseButton.Right && _grabbing == GrabMode.Move)
                {
                    _curve.SetPointPosition(_selectedIndex, _initialGrabPos);
                    SetSelectedIndex(_initialGrabIndex);
                    _hoveredIndex = GetPointAt(mpos);
                    _grabbing = GrabMode.None;
                }
                else
                {
                    _selectedTangentIndex = GetTangentAt(mpos);
                    if (_selectedTangentIndex != TangentIndex.None)
                    {
                        ResetLinear(_selectedIndex, _selectedTangentIndex);
                    }
                    else
                    {
                        int pointToRemove = GetPointAt(mpos);
                        if (pointToRemove == -1 || _curve.Count <= 1)
                        {
                            SetSelectedIndex(-1);
                        }
                        else
                        {
                            if (_grabbing == GrabMode.Add)
                            {
                                _curve.RemovePoint(pointToRemove);
                                SetSelectedIndex(-1);
                            }
                            else
                            {
                                RemovePoint(pointToRemove);
                            }
                            _hoveredIndex = GetPointAt(mpos);
                            _grabbing = GrabMode.None;
                        }
                    }
                }
            }

            // Selecting or creating points.
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (_grabbing == GrabMode.None)
                {
                    _selectedTangentIndex = GetTangentAt(mpos);
                    if (_selectedTangentIndex == TangentIndex.None)
                    {
                        SetSelectedIndex(GetPointAt(mpos));
                    }
                    QueueRedraw();
                }

                if (_selectedIndex != -1)
                {
                    _grabbing = GrabMode.Move;
                    _initialGrabPos = _curve.GetPointPosition(_selectedIndex);
                    _initialGrabIndex = _selectedIndex;
                    if (_selectedIndex > 0)
                        _initialHandleMode = _curve.GetPoint(_selectedIndex).EstimatedHandleMode;
                    if (_selectedIndex < _curve.Count - 1)
                        _initialHandleMode = _curve.GetPoint(_selectedIndex).EstimatedHandleMode;
                }
                else if (_grabbing == GrabMode.None)
                {
                    Vector2 newPos = GetWorldPos(mpos).Clamp(
                        new Vector2(MinDomain, MinValue),
                        new Vector2(MaxDomain, MaxValue)
                    );

                    newPos.X = GetOffsetWithoutCollision(_selectedIndex, newPos.X, mpos.X >= GetViewPos(newPos).X);
                    var p = _curve.GetClosestPoint(newPos, out var t);
                    if (_curve.Count == 1)
                    {
                        int idx = 1;
                        if (newPos.X < p.X) idx = 0;
                        _curve.AddPoint(new(newPos.X, p.Y), new(-0.1f, 0), new(0.1f, 0), idx);
                        SetSelectedIndex(idx);
                        _grabbing = GrabMode.Add;
                        _initialGrabPos = newPos;
                    }
                    else if (p.DistanceTo(newPos) < DomainRange / 83.0f) // Can split
                    {
                        var idx = _curve.TryInsertPoint(t);
                        SetSelectedIndex(idx);
                        _grabbing = GrabMode.Add;
                        _initialGrabPos = newPos;
                    }
                }
            }
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            if (_selectedTangentIndex != TangentIndex.None)
            {
                _grabbing = GrabMode.None;
            }
            else if (_grabbing == GrabMode.Move)
            {
                SetPointPosition(_selectedIndex, _curve.GetPointPosition(_selectedIndex));
                _grabbing = GrabMode.None;
            }
            else if (_grabbing == GrabMode.Add)
            {
                _grabbing = GrabMode.None;
            }

            QueueRedraw();
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            Vector2 mpos = mouseMotion.Position;

            if (_grabbing != GrabMode.None && _curve != null)
            {
                if (_selectedIndex != -1)
                {
                    if (_selectedTangentIndex == TangentIndex.None)
                    {
                        Vector2 newPos = GetWorldPos(mpos).Clamp(
                            new Vector2(MinDomain, MinValue),
                            new Vector2(MaxDomain, MaxValue)
                        );

                        newPos.X = GetOffsetWithoutCollision(_selectedIndex, newPos.X, mpos.X >= GetViewPos(newPos).X);
                        newPos.Y = Mathf.Clamp(newPos.Y, MinValue, MaxValue);
                        var oldPos = _curve.GetPointPosition(_selectedIndex);
                        _curve.SetPointPosition(_selectedIndex, newPos);
                        if (!_curve.IsXMonotone) _curve.SetPointPosition(_selectedIndex, oldPos);
                    }
                    else
                    {
                        Vector2 newPos = GetWorldPos(mpos).Clamp(
                            new Vector2(MinDomain, MinValue),
                            new Vector2(MaxDomain, MaxValue)
                        ) - _curve.GetPointPosition(_selectedIndex);

                        if (_selectedTangentIndex == TangentIndex.Left)
                        {
                            // Shit code but work
                            var oldPos = _curve.Points.ToArray();
                            if (Input.IsKeyPressed(Key.Alt) || _initialHandleMode == BezierCurve.HandleControlMode.Free)
                                _curve.SetPointIn(_selectedIndex, newPos);
                            else
                                _curve.SetPointInLinearly(_selectedIndex, newPos);

                            if (!_curve.IsXMonotone)
                                _curve.Points = oldPos.ToList();
                        }
                        else
                        {
                            var oldPos = _curve.Points.ToArray();
                            if (Input.IsKeyPressed(Key.Alt) || _initialHandleMode == BezierCurve.HandleControlMode.Free)
                                _curve.SetPointOut(_selectedIndex, newPos);
                            else
                                _curve.SetPointOutLinearly(_selectedIndex, newPos);

                            if (!_curve.IsXMonotone)
                                _curve.Points = oldPos.ToList();
                        }
                    }
                }
            }
            else
            {
                _hoveredIndex = GetPointAt(mpos);
                _hoveredTangentIndex = GetTangentAt(mpos);
                QueueRedraw();
            }
        }

        _CurveChanged();
    }

    private void _CurveChanged()
    {
        QueueRedraw();
        if (_selectedIndex >= _curve.Count)
            SetSelectedIndex(-1);
    }

    private int GetPointAt(Vector2 pos)
    {
        if (_curve == null)
            return -1;

        Rect2 hoverRect = new Rect2(pos, Vector2.Zero).Grow(_hoverRadius);
        int closestIdx = -1;
        float closestDistSquared = _hoverRadius * _hoverRadius * 2;

        for (int i = 0; i < _curve.Count; i++)
        {
            Vector2 p = GetViewPos(_curve.GetPointPosition(i));
            if (hoverRect.HasPoint(p) && p.DistanceSquaredTo(pos) < closestDistSquared)
            {
                closestDistSquared = p.DistanceSquaredTo(pos);
                closestIdx = i;
            }
        }

        return closestIdx;
    }

    private TangentIndex GetTangentAt(Vector2 pos)
    {
        if (_curve == null || _selectedIndex < 0)
            return TangentIndex.None;

        Rect2 hoverRect = new Rect2(pos, Vector2.Zero).Grow(_tangentHoverRadius);

        if (_selectedIndex != 0)
        {
            Vector2 controlPos = GetTangentViewPos(_selectedIndex, TangentIndex.Left);
            if (hoverRect.HasPoint(controlPos))
                return TangentIndex.Left;
        }

        if (_selectedIndex != _curve.Count - 1)
        {
            Vector2 controlPos = GetTangentViewPos(_selectedIndex, TangentIndex.Right);
            if (hoverRect.HasPoint(controlPos))
                return TangentIndex.Right;
        }

        return TangentIndex.None;
    }

    private float GetOffsetWithoutCollision(int currentIndex, float offset, bool prioritizeRight)
    {
        float safeOffset = offset;
        bool prioritizingRight = prioritizeRight;

        for (int i = 0; i < _curve.Count; i++)
        {
            if (i == currentIndex)
                continue;

            if (_curve.GetPointPosition(i).X > safeOffset)
                break;

            if (Mathf.IsEqualApprox(_curve.GetPointPosition(i).X, safeOffset))
            {
                if (prioritizingRight)
                {
                    safeOffset += 0.00001f;
                    if (safeOffset > 1.0f)
                    {
                        safeOffset = 1.0f;
                        prioritizingRight = false;
                    }
                }
                else
                {
                    safeOffset -= 0.00001f;
                    if (safeOffset < 0.0f)
                    {
                        safeOffset = 0.0f;
                        prioritizingRight = true;
                    }
                }
                i = -1;
            }
        }

        return safeOffset;
    }

    private void AddPoint(Vector2 pos)
    {
        if (_curve == null)
            return;

        // Note: Undo/redo not supported, so we add the point directly
        _curve.AddPoint(pos, new(-0.1f, 0), new(0.1f, 0));
        SetSelectedIndex(_curve.Count - 1);
    }

    private void RemovePoint(int index)
    {
        if (_curve == null || index < 0 || index >= _curve.Count)
            return;

        // Note: Undo/redo not supported, so we remove the point directly
        int newSelectedIndex = _selectedIndex;
        if (newSelectedIndex > index)
            newSelectedIndex -= 1;
        else if (newSelectedIndex == index)
            newSelectedIndex = -1;

        _curve.RemovePoint(index);
        SetSelectedIndex(newSelectedIndex);
    }

    private void SetPointPosition(int index, Vector2 pos)
    {
        if (_curve == null || index < 0 || index >= _curve.Count)
            return;

        if (_initialGrabPos == pos)
            return;

        _curve.SetPointPosition(index, pos);
        SetSelectedIndex(index);
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        return "[Mapping Curve Tooltip]";
    }

    private void ResetLinear(int index, TangentIndex tangent)
    {
        if (_curve == null || index < 0 || index >= _curve.Count || tangent == TangentIndex.None)
            return;

        var point = _curve.GetPoint(index);
        var prevMode = point.EstimatedHandleMode;
        BezierCurve.HandleControlMode mode;
        if (prevMode == BezierCurve.HandleControlMode.Linear)
            mode = BezierCurve.HandleControlMode.LinearEqual;
        else if (prevMode == BezierCurve.HandleControlMode.Free)
            mode = BezierCurve.HandleControlMode.Linear;
        else
            return;

        if (mode == BezierCurve.HandleControlMode.LinearEqual)
        {
            if (tangent == TangentIndex.Left)
                _curve.SetPointIn(index, -_curve.GetPoint(index).Out);
            else
                _curve.SetPointOut(index, -_curve.GetPoint(index).In);
        }

        if (mode == BezierCurve.HandleControlMode.Linear)
        {
            if (tangent == TangentIndex.Left)
                _curve.SetPointInTangent(index, _curve.GetPointOutTangent(index));
            else
                _curve.SetPointOutTangent(index, _curve.GetPointInTangent(index));
        }
    }

    private void SetSelectedIndex(int index)
    {
        if (_selectedIndex != index)
        {
            _selectedIndex = index;
            QueueRedraw();
        }
    }

    private void UpdateViewTransform()
    {
        float fontSize = (int)(GetThemeFontSize("font_size", "Label") * 0.8f);
        float margin = fontSize + 8;

        Rect2 worldRect = new Rect2(MinDomain, MinValue, DomainRange, ValueRange);
        Vector2 viewMargin = new Vector2(margin, margin);
        Vector2 viewSize = Size - viewMargin * 2;
        Vector2 scale = viewSize / worldRect.Size;

        Transform2D worldTrans = Transform2D.Identity;
        worldTrans = worldTrans.Translated(-worldRect.Position - new Vector2(0, worldRect.Size.Y));
        worldTrans = worldTrans.Scaled(new Vector2(scale.X, -scale.Y));

        Transform2D viewTrans = Transform2D.Identity;
        viewTrans = viewTrans.Translated(viewMargin);

        _worldToView = viewTrans * worldTrans;
    }

    private Vector2 GetTangentViewPos(int index, TangentIndex tangent)
    {
        var tanPos = _curve.GetPointPosition(index) +
                     (tangent == TangentIndex.Left ? _curve.GetPoint(index).In : _curve.GetPoint(index).Out);
        return GetViewPos(tanPos);
    }

    private Vector2 GetViewPos(Vector2 worldPos)
    {
        return _worldToView * worldPos;
    }

    private Vector2 GetWorldPos(Vector2 viewPos)
    {
        return _worldToView.AffineInverse() * viewPos;
    }

    private void PlotCurveAccurate(float step, Color lineColor, Color edgeLineColor)
    {
        if (_curve.Count <= 1)
        {
            float y = _curve.SampleX(0);
            DrawLine(GetViewPos(new Vector2(MinDomain, y)) + new Vector2(0.5f, 0), GetViewPos(new Vector2(MaxDomain, y)) - new Vector2(1.5f, 0), lineColor, LineWidth, true);
            return;
        }

        int nSample = 128;
        List<Vector2> samples = new(nSample);
        for (int i = 0; i < nSample; i++)
        {
            float x = MinDomain + i * (DomainRange / (nSample - 1));
            samples.Add(new(x, _curve.SampleX(x)));
        }
        for (int i = 1; i < nSample; i++)
        {
            DrawLine(GetViewPos(samples[i - 1]), GetViewPos(samples[i]), lineColor, LineWidth, true);
        }
    }


    public override void _Draw()
    {
        if (_curve == null)
            return;
        //// Godot 4.4.1 bug: UpdateMinimumSize causes split container crash. So change the container into box.
        //// More bugs: This also causes crash when one is visible and another is not.
        // Use a aspect ratio container to fix this.

        // UpdateMinimumSize();
        UpdateViewTransform();

        // Draw background
        DrawStyleBox(GetThemeStylebox("panel", "Tree"), new Rect2(Vector2.Zero, Size));

        // Draw primary grid
        DrawSetTransformMatrix(_worldToView);

        Vector2 minEdge = GetWorldPos(new Vector2(0, Size.Y));
        Vector2 maxEdge = GetWorldPos(new Vector2(Size.X, 0));

        Color gridColorPrimary = GetThemeColor("font_color", "Label") * new Color(1, 1, 1, 0.25f);
        Color gridColor = GetThemeColor("font_color", "Label") * new Color(1, 1, 1, 0.1f);

        Vector2I gridSteps = new Vector2I(4, 2);
        Vector2 stepSize = new Vector2(DomainRange, ValueRange) / gridSteps;

        DrawLine(new Vector2(minEdge.X, MinValue), new Vector2(maxEdge.X, MinValue), gridColorPrimary);
        DrawLine(new Vector2(maxEdge.X, MaxValue), new Vector2(minEdge.X, MaxValue), gridColorPrimary);
        DrawLine(new Vector2(MinDomain, minEdge.Y), new Vector2(MinDomain, maxEdge.Y), gridColorPrimary);
        DrawLine(new Vector2(MaxDomain, maxEdge.Y), new Vector2(MaxDomain, minEdge.Y), gridColorPrimary);

        for (int i = 1; i < gridSteps.X; i++)
        {
            float x = MinDomain + i * stepSize.X;
            DrawLine(new Vector2(x, minEdge.Y), new Vector2(x, maxEdge.Y), gridColor);
        }

        for (int i = 1; i < gridSteps.Y; i++)
        {
            float y = MinValue + i * stepSize.Y;
            DrawLine(new Vector2(minEdge.X, y), new Vector2(maxEdge.X, y), gridColor);
        }

        // Draw number markings
        DrawSetTransformMatrix(Transform2D.Identity);

        Font font = GetThemeFont("font", "Label");
        int fontSize = (int)(GetThemeFontSize("font_size", "Label") * 0.8f);
        float fontHeight = font.GetHeight(fontSize);
        Color textColor = GetThemeColor("font_color", "Label");

        int pad = (int)Mathf.Round(2);

        for (int i = 0; i <= gridSteps.X; i++)
        {
            float x = MinDomain + i * stepSize.X;
            DrawString(font, GetViewPos(new Vector2(x, MinValue)) + new Vector2(pad, fontHeight - pad), x.ToString("F2"), HorizontalAlignment.Center, -1, fontSize, textColor);
        }

        for (int i = 0; i <= gridSteps.Y; i++)
        {
            float y = MinValue + i * stepSize.Y;
            DrawString(font, GetViewPos(new Vector2(MinDomain, y)) + new Vector2(pad, -pad), y.ToString("F2"), HorizontalAlignment.Left, -1, fontSize, textColor);
        }

        // Draw curve
        Color lineColor = GetThemeColor("font_color", "Label");
        Color edgeLineColor = GetThemeColor("font_color", "Label") * new Color(1, 1, 1, 0.75f);

        PlotCurveAccurate(StepSize, lineColor, edgeLineColor);

        // Draw points
        bool altPressed = Input.IsKeyPressed(Key.Alt);

        Color pointColor = GetThemeColor("font_color", "Label");

        for (int i = 0; i < _curve.Count; i++)
        {
            Vector2 pos = GetViewPos(_curve.GetPointPosition(i));
            if (_selectedIndex != i)
            {
                DrawRect(new Rect2(pos, Vector2.Zero).Grow(_pointRadius), pointColor);
            }
            if (_hoveredIndex == i && _hoveredTangentIndex == TangentIndex.None)
            {
                DrawRect(new Rect2(pos, Vector2.Zero).Grow(_hoverRadius - Mathf.Round(3)), lineColor, false, Mathf.Round(1));
            }
        }

        // Draw selected point and tangents
        if (_selectedIndex >= 0)
        {
            Vector2 pointPos = _curve.GetPointPosition(_selectedIndex);
            Color selectedPointColor = GetThemeColor("font_color", "Label");

            if (_grabbing == GrabMode.None || _initialGrabPos == pointPos || _selectedTangentIndex != TangentIndex.None)
            {
                Color selectedTangentColor = GetThemeColor("font_color", "Label").Darkened(0.25f);
                Color tangentColor = GetThemeColor("font_color", "Label").Darkened(0.25f);

                if (_selectedIndex != 0)
                {
                    Vector2 controlPos = GetTangentViewPos(_selectedIndex, TangentIndex.Left);
                    Color leftTangentColor = _selectedTangentIndex == TangentIndex.Left ? selectedTangentColor : tangentColor;

                    DrawLine(GetViewPos(pointPos), controlPos, leftTangentColor, 0.5f, true);
                    DrawRect(new Rect2(controlPos, Vector2.Zero).Grow(_tangentRadius), leftTangentColor);

                    var mode = _curve.GetPoint(_selectedIndex).EstimatedHandleMode;
                    bool isLinear = mode == BezierCurve.HandleControlMode.Linear || mode == BezierCurve.HandleControlMode.LinearEqual;
                    if (_hoveredTangentIndex == TangentIndex.Left || (_hoveredTangentIndex == TangentIndex.Right && !altPressed && isLinear))
                    {
                        DrawRect(new Rect2(controlPos, Vector2.Zero).Grow(_tangentHoverRadius - Mathf.Round(3)), tangentColor, false, Mathf.Round(1));
                    }
                }

                if (_selectedIndex != _curve.Count - 1)
                {
                    Vector2 controlPos = GetTangentViewPos(_selectedIndex, TangentIndex.Right);
                    Color rightTangentColor = _selectedTangentIndex == TangentIndex.Right ? selectedTangentColor : tangentColor;

                    DrawLine(GetViewPos(pointPos), controlPos, rightTangentColor, 0.5f, true);
                    DrawRect(new Rect2(controlPos, Vector2.Zero).Grow(_tangentRadius), rightTangentColor);

                    var mode = _curve.GetPoint(_selectedIndex).EstimatedHandleMode;
                    bool isLinear = mode == BezierCurve.HandleControlMode.Linear || mode == BezierCurve.HandleControlMode.LinearEqual;
                    if (_hoveredTangentIndex == TangentIndex.Right || (_hoveredTangentIndex == TangentIndex.Left && !altPressed && isLinear))
                    {
                        DrawRect(new Rect2(controlPos, Vector2.Zero).Grow(_tangentHoverRadius - Mathf.Round(3)), tangentColor, false, Mathf.Round(1));
                    }
                }
            }

            DrawRect(new Rect2(GetViewPos(pointPos), Vector2.Zero).Grow(_pointRadius), selectedPointColor);
        }

        // Draw help Samsung Electronics
        if (_selectedIndex > 0 && _selectedIndex < _curve.Count - 1 && _selectedTangentIndex == TangentIndex.None && _hoveredTangentIndex != TangentIndex.None && !altPressed)
        {
            float width = Size.X - 50;
            textColor.A *= 0.4f;
            DrawMultilineString(font, new Vector2(25, fontHeight - Mathf.Round(2)), "", HorizontalAlignment.Center, width, fontSize, -1, textColor);
        }
        else if (_selectedIndex != -1 && _selectedTangentIndex == TangentIndex.None)
        {
            Vector2 pointPos = _curve.GetPointPosition(_selectedIndex);
            float width = Size.X - 50;
            textColor.A *= 0.8f;
            DrawString(font, new Vector2(25, fontHeight - Mathf.Round(2)), $"({pointPos.X:F2}, {pointPos.Y:F2})", HorizontalAlignment.Center, width, fontSize, textColor);
        }
        else if (_selectedIndex != -1 && _selectedTangentIndex != TangentIndex.None)
        {
            float width = Size.X - 50;
            textColor.A *= 0.8f;
            float theta = Mathf.RadToDeg(Mathf.Atan(_selectedTangentIndex == TangentIndex.Left ? -_curve.GetPointInTangent(_selectedIndex) : _curve.GetPointOutTangent(_selectedIndex)));
            DrawString(font, new Vector2(25, fontHeight - Mathf.Round(2)), $"{theta:F1} °", HorizontalAlignment.Center, width, fontSize, textColor);
        }

        // Draw constraints
        DrawSetTransformMatrix(_worldToView);

        if (Input.IsKeyPressed(Key.Alt) && _grabbing != GrabMode.None && _selectedTangentIndex == TangentIndex.None)
        {
            float prevPointOffset = _selectedIndex > 0 ? _curve.GetPointPosition(_selectedIndex - 1).X : MinDomain;
            float nextPointOffset = _selectedIndex < _curve.Count - 1 ? _curve.GetPointPosition(_selectedIndex + 1).X : MaxDomain;

            DrawLine(new Vector2(prevPointOffset, MinValue), new Vector2(prevPointOffset, MaxValue), new Color(pointColor, 0.6f));
            DrawLine(new Vector2(nextPointOffset, MinValue), new Vector2(nextPointOffset, MaxValue), new Color(pointColor, 0.6f));
        }

        if (altPressed && _grabbing != GrabMode.None && _selectedTangentIndex == TangentIndex.None)
        {
            DrawLine(new Vector2(_initialGrabPos.X, MinValue), new Vector2(_initialGrabPos.X, MaxValue), GetThemeColor("font_color", "Label").Darkened(0.4f));
            DrawLine(new Vector2(MinDomain, _initialGrabPos.Y), new Vector2(MaxDomain, _initialGrabPos.Y), GetThemeColor("font_color", "Label").Darkened(0.4f));
        }
    }
}