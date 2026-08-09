using System;
using System.Linq;
using Ciallo.Widget;
using Frent;
using Godot;

namespace Ciallo.Tool;

/// <summary>
/// Base for the classes that handle canvas interactions. Tools hold one or more of these interactive session.
/// The actual place implementing canvas interaction behaviors.
/// </summary>
/// <remarks>
/// Key design ideas:
/// - Separating interactive logic from how to trigger an interactive session。This allows us to support key remapping and more.
/// E.g. Stroke drag interactor should not know it's started by dragging left mouse button, or pressing the 'G' key(like Blender)
/// The tool scripts implementing ToolBase are responsible for configuring state machine.
///
/// </remarks>
public abstract class InteractiveSessionBase
{
    public ToolBase Tool;
    public Entity Document { get; set; }
    public Entity[] WorkingLayers;
    public Entity WorkingLayer => WorkingLayers.Single();
    /// <summary>
    /// Tell the tool to throttle update interval in this interactive session.
    /// Set this to 0 if need raw input data. 
    /// </summary>
    /// <remarks>
    /// Multiple input in one frame could cause Godot stutter.
    /// E.g. 144FPS screen, 1000Hz mouse report rate, dragging mouse could cause 6-7 input events in one frame
    /// Directly calling Polygon2D.SetPolygon to set 500 points in one frame will stutter godot. 
    /// </remarks>
    public TimeSpan MovingMinInterval = TimeSpan.FromMilliseconds(5);

    public virtual void BeforeTransitionSrcEnd(InteractiveSessionBase src) { }
    public abstract void Start(CursorButtonData data);
    public abstract void Moving(CursorMotionData data);
    public abstract void End(CursorButtonData data);
    public abstract void Cancel();
    public abstract bool OnKey(InputEventKey key, CursorButtonData data);
    public virtual bool OnMouseButton(InputEventMouseButton button, CursorButtonData data) => false;
    public virtual void DrawProperty(PropertyContainer container) { }
    public virtual void Refresh(CursorButtonData data = default) // Suppose to only be called by hover sessions.
    {
        Cancel();
        Start(data);
    }
}

public abstract class ActiveInteractionSessionBase : InteractiveSessionBase
{
    public override bool OnKey(InputEventKey key, CursorButtonData data) => true;
    public override bool OnMouseButton(InputEventMouseButton button, CursorButtonData data) => true;
}
