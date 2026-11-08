using Ciallo.Data;
using Godot;
using R3;

namespace Ciallo.GuiControl;

public partial class ColorPanel : ScrollContainer
{
    public override void _Ready()
    {
        AppDocumentManager.WorkingDocument.Subscribe(document =>
        {
            this.QueueFreeChildren();
            if (document.IsNull) return;

            var picker = new ColorPicker
            {
                Name = "CurrentColorPicker",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            DocumentColorControls.BindCurrentColor(picker, document);
            AddChild(picker);
        }).AddTo(this);
    }
}
