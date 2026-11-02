using Godot;

namespace Ciallo.GuiControl;

public partial class RecentFilesTree : Tree
{
    public override int _GetCursorShape(Vector2 atPosition) =>
        (int)(GetItemAtPosition(atPosition) is null ? CursorShape.Arrow : CursorShape.PointingHand);
}
