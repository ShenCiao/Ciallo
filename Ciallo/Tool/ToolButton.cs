using System;
using Ciallo.Command;
using R3;

namespace Ciallo.Tool;

// Application-level tool-button catalog. Source generator reads enum declaration order, emits
// Definitions/GetDefinition/TryResolveHotkey, and reports errors.
public static partial class ToolButton
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DefinitionAttribute(
        string iconPath,
        string tooltip) : Attribute
    {
        public string IconPath { get; } = iconPath;
        public string Tooltip { get; } = tooltip;

        // nameof keeps AppHotkeys member compile-checked; generator emits strongly typed access.
        public string? ShortcutMember { get; set; }
    }

    public enum Type
    {
        // Stable persistence identifiers, independent of declaration order. Keep 0 undefined so
        // default(Type) cannot silently mean a real button.
        [Definition(
            "res://Icon/cursor-default-outline.svg",
            "Selection",
            ShortcutMember = nameof(AppHotkeys.Global.ToolSelection))]
        Select = 1,

        [Definition(
            "res://Icon/brush.svg",
            "Paint Brush",
            ShortcutMember = nameof(AppHotkeys.Global.ToolPaintBrush))]
        PaintStroke = 2,

        [Definition(
            "res://Icon/lasso-fill.svg",
            "Paint Fill",
            ShortcutMember = nameof(AppHotkeys.Global.ToolPaintFill))]
        PaintFill = 3,

        [Definition("res://Icon/bucket-fill-marker.svg", "Vector Fill")]
        VectorFill = 4,

        [Definition("res://Icon/water-drop.svg", "Liquify")]
        Liquify = 5,

        [Definition("res://Icon/scissor.svg", "Trim")]
        Trim = 6,

        [Definition("res://Icon/wrench.svg", "Gap Bridge")]
        GapBridge = 7,
    }

    // A button without ShortcutMember has no Hotkey (nullable). TryResolveHotkey skips it.
    public sealed record Descriptor(
        Type Type,
        string IconPath,
        string Tooltip,
        Hotkey Shortcut);

    // User selection state. null is valid "no latched tool". Panel observes; RequestTool is sole writer.
    // Nullable type is resevered for future tools without a tool button (this will be light table related functionality), cannot be null now
    public static readonly ReactiveProperty<Type?> ActiveToolButton = new(Type.PaintStroke);
}
