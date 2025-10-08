using Ciallo.Data;
using Ciallo.NodeControl;
using Massive;

namespace Ciallo.Tool;

/// <summary>
/// Base class for the objects that handle canvas interactions. Tools hold one or more of these interactors.
/// Interactors actually implement users' tools logics and behaviors.
/// In order to support key remapping, interactors shouldn't know "how to start himself".
/// E.g. Stroke drag interactor doesn't know it's started by dragging left mouse button, or pressing the 'G' key(like Blender)
/// </summary>
public abstract class InteractorBase
{
    public World WorkingWorld => AppWorldManager.WorkingWorld.Value;
    public Entity Document => WorkingWorld.Document();
    public SelectionManager SelectionManager => Document.Get<SelectionManager>();
    
    public abstract bool CanInteract { get; }

    public virtual void Start(CursorButtonData data) { }
    
    public virtual void Interacting(CursorMotionData data) { }

    public virtual void End(CursorButtonData data) { }

    public virtual void Cancel() { }
}