using Godot;

namespace Ciallo.GuiControl;

public partial class RecentFilesTree : Tree
{
    private int _columnWidth = -1;
    private bool _refreshQueued;

    public override void _Ready()
    {
        ThemeChanged += RefreshPathLabels;
        Draw += () =>
        {
            if (GetColumnWidth(0) != _columnWidth) RefreshPathLabels();
        };
    }

    public override int _GetCursorShape(Vector2 atPosition) =>
        (int)(GetItemAtPosition(atPosition) is null ? CursorShape.Arrow : CursorShape.PointingHand);

    public void RefreshPathLabels()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Callable.From(UpdatePathLabels).CallDeferred();
    }

    private void UpdatePathLabels()
    {
        _refreshQueued = false;
        _columnWidth = GetColumnWidth(0);
        var font = GetThemeFont("font");
        var fontSize = GetThemeFontSize("font_size");
        var textWidth = _columnWidth - GetThemeConstant("h_separation")
            - GetThemeConstant("inner_item_margin_left") - GetThemeConstant("inner_item_margin_right")
            - GetThemeStylebox("button_pressed").GetMinimumSize().X - GetThemeConstant("button_margin");

        foreach (var item in GetRoot().GetChildren())
        {
            var path = item.GetMetadata(0).AsString();
            var label = FormatPath(path, textWidth - item.GetButton(0, 0).GetWidth(), font, fontSize);
            if (item.GetText(0) != label) item.SetText(0, label);
        }
    }

    private static string FormatPath(string path, float width, Font font, int fontSize)
    {
        var file = path.GetFile();
        var name = file.GetBaseName();
        var directory = path[..^file.Length];
        var fullLabel = directory + name;
        if (Fits(fullLabel)) return fullLabel;

        for (var i = directory.Length - 2; i >= 0; i--)
        {
            if (directory[i] is not ('/' or '\\')) continue;
            var label = directory[..(i + 1)] + "…" + directory[^1] + name;
            if (Fits(label)) return label;
        }

        if (directory.Length > 0)
        {
            var label = "…" + directory[^1] + name;
            if (Fits(label)) return label;
        }
        return name;

        bool Fits(string text) => font.GetStringSize(text, fontSize: fontSize).X <= width;
    }
}
