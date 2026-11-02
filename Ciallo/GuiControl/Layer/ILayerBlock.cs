using Ciallo.Widget;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

/// <summary>
/// UI contract for layer-tree header controls shared by the Layer panel and Timeline header.
/// </summary>
public interface ILayerBlock
{
    Entity LayerEntity { get; }
    LayerWrapper Wrapper { get; }
    Container Node { get; }
    bool IsFolder { get; }
    bool IsCelFolder { get; }
    CheckBox VisibleButton { get; }
    CheckButton WorkingButton { get; }
    CheckButton DropdownArrow { get; }
    ColorRect ClippingMaskIndicator { get; }
    LabelLineEdit LabelLineEdit { get; }
    Indent Indent { get; }
}
