using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ciallo.Data;

internal sealed class ProjectFormatRegistry
{
    public static ProjectFormatRegistry Shared { get; } = Create();

    public IReadOnlyList<ComponentDescriptor> Components { get; }

    private ProjectFormatRegistry(IReadOnlyList<ComponentDescriptor> components)
    {
        Components = components;
    }

    public static ProjectFormatRegistry Create()
    {
        var components = AppDocumentManager.ToSerializeComponents
            .OrderBy(ComponentDescriptor.GetStorageName)
            .Select(ComponentDescriptor.Create)
            .ToArray();
        return new ProjectFormatRegistry(components);
    }
}

internal sealed class ComponentDescriptor
{
    public Type ComponentType { get; }
    public string Name { get; }
    public string TableName => "component_" + Name;
    public IReadOnlyList<FieldDescriptor> Fields { get; private set; }

    private ComponentDescriptor(Type componentType, string name, IReadOnlyList<FieldDescriptor> fields)
    {
        ComponentType = componentType;
        Name = name;
        Fields = fields;
    }

    public static ComponentDescriptor Create(Type type)
    {
        var name = GetStorageName(type);
        // Two-pass: build the descriptor first, then populate its fields so every
        // FieldDescriptor.Component points to the real (non-dummy) instance.
        var descriptor = new ComponentDescriptor(type, name, []);
        var fields = EnumerateFields(type)
            .Select(field => FieldDescriptor.TryCreate(descriptor, field))
            .Where(field => field != null)
            .ToArray();
        descriptor.Fields = fields;
        return descriptor;
    }

    public static string GetStorageName(Type type)
    {
        var attr = type.GetCustomAttribute<ToSerializeAttribute>();
        return string.IsNullOrWhiteSpace(attr?.Name) ? type.Name : attr.Name;
    }

    // Walk the base-class chain so inherited persisted fields (e.g. EntityTreeNode's Parent/Children
    // on LayerTreeNode) are included.
    private static IEnumerable<FieldInfo> EnumerateFields(Type type)
    {
        for (var cursor = type; cursor != null && cursor != typeof(object); cursor = cursor.BaseType)
        {
            foreach (var field in cursor.GetFields(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
                yield return field;
        }
    }
}
