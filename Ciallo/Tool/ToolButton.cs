using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Ciallo.Command;
using Godot;
using R3;

namespace Ciallo.Tool;

// Application-level tool-button catalog. Definitions initialized via reflection.
public static class ToolButton
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DefinitionAttribute(
        string iconPath,
        string tooltip) : Attribute
    {
        public string IconPath { get; } = iconPath;
        public string Tooltip { get; } = tooltip;

        // nameof keeps AppHotkeys member compile-checked; generator emits strongly typed access.
        public string ShortcutMember { get; set; }
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

    public static ImmutableArray<Descriptor> Definitions { get; } = InitializeDefinitions();

    private static ImmutableArray<Descriptor> InitializeDefinitions()
    {
        return typeof(Type)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.GetCustomAttribute<DefinitionAttribute>() != null)
            .OrderBy(f => f.MetadataToken)
            .Select(f =>
            {
                var type = (Type)f.GetValue(null)!;
                var attr = f.GetCustomAttribute<DefinitionAttribute>()!;

                Hotkey shortcut = null;
                if (!string.IsNullOrEmpty(attr.ShortcutMember))
                {
                    var hotkeyField = typeof(AppHotkeys.Global).GetField(attr.ShortcutMember, BindingFlags.Public | BindingFlags.Static);
                    shortcut = (Hotkey)hotkeyField?.GetValue(null);
                }

                return new Descriptor(type, attr.IconPath, attr.Tooltip, shortcut);
            })
            .ToImmutableArray();
    }

    public static Descriptor GetDefinition(Type type)
    {
        foreach (var def in Definitions)
        {
            if (def.Type == type)
                return def;
        }
        throw new ArgumentOutOfRangeException(nameof(type), type, null);
    }

    public static bool TryResolveHotkey(InputEvent key, out Type type)
    {
        foreach (var definition in Definitions)
        {
            if (definition.Shortcut?.IsPressedBy(key) == true)
            {
                type = definition.Type;
                return true;
            }
        }

        type = default;
        return false;
    }

    // User selection state. null is valid "no latched tool". Panel observes; RequestTool is sole writer.
    // Nullable type is resevered for future tools without a tool button (this will be light table related functionality), cannot be null now
    public static readonly ReactiveProperty<Type?> ActiveToolButton = new(Type.PaintStroke);
}
