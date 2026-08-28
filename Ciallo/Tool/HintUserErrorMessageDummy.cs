using Ciallo.Rendering;
using Ciallo.Widget;
using Godot;

namespace Ciallo.Tool;

public class HintUserErrorMessageDummy : Interaction, IPropertyProvider
{
    private Label _label;

    public string Message
    {
        get;
        set
        {
            field = value;
            _label?.Text = FormatMessage(value);
        }
    }

    public HintUserErrorMessageDummy(string msg = "")
    {
        Message = msg;
    }

    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.Forbidden;
    }
    public override void Moving(CursorMotionData data) { }
    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }
    public override bool OnKey(InputEventKey key, CursorButtonData data) => true;

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        _label = new() { Text = FormatMessage(Message) };
        container.AddChild(_label);
    }

    private static string FormatMessage(string message) => "⚠ " + message.Tr();
}
