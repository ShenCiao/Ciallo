using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Frent;
using ObservableCollections;
using R3;

namespace Ciallo.Data;

/// <summary>
/// How one persisted field maps onto a single DuckDB column.
/// Every field is exactly one column; there are no side tables in the DuckDB format.
/// </summary>
internal enum FieldShape
{
    Scalar,         // native column: VARCHAR / BOOLEAN / INTEGER / FLOAT / ...
    Struct,         // STRUCT column via a StructCodec (Color, Vector2, Transform2D, BezierPoint)
    StructArray,    // STRUCT[] column (e.g. stroke positions, bezier curves)
    PrimitiveArray, // scalar list column: FLOAT[] / INTEGER[] / ...
    EntityRef,      // INTEGER, nullable (entity id, or NULL for Entity.Null)
    EntityArray,    // INTEGER[] (entity list or set)
    EntityMap,      // MAP(INTEGER, INTEGER), nullable (int-keyed entity map, e.g. Exposures)
    Blob,           // BLOB via MessagePack (true binary media only)
}

/// <summary>Concrete collection type a value/entity array field uses, for reconstruction on read.</summary>
internal enum ContainerKind
{
    None,
    ImmutableArray,
    List,
    ObservableList,
    HashSet,
    ObservableHashSet,
    Array,
}

internal sealed class FieldDescriptor
{
    public ComponentDescriptor Component { get; }
    public FieldInfo Field { get; }
    public string Name { get; }
    public StorageKind StorageKind { get; }
    public EntityNullability EntityNullability { get; }
    public FieldShape Shape { get; }
    public Type FieldType { get; }
    public Type ValueType { get; }
    public Type NonNullableValueType { get; }
    public Type ElementType { get; }
    public ContainerKind ContainerKind { get; }
    public StructCodec Codec { get; }
    public bool IsReactive { get; }
    public bool IsNullable { get; }
    public string DuckDbColumnType { get; }

    // Field access goes through delegates compiled once at startup instead of per-row reflection,
    // which showed up under save/load of large documents.
    private readonly Func<object, object> _getProjectValue;
    private readonly Func<object, object> _getFieldStorage;
    private readonly Action<object, object> _setProjectValue;
    private readonly Action<object, object> _setFieldStorage;

    private FieldDescriptor(
        ComponentDescriptor component,
        FieldInfo field,
        ProjectFieldAttribute attr,
        FieldShape shape,
        Type fieldType,
        Type valueType,
        Type nonNullableValueType,
        Type elementType,
        ContainerKind containerKind,
        StructCodec codec,
        bool isReactive,
        bool isNullable)
    {
        Component = component;
        Field = field;
        Name = string.IsNullOrWhiteSpace(attr.Name) ? field.Name : attr.Name;
        StorageKind = attr.StorageKind;
        EntityNullability = attr.EntityNullability;
        Shape = shape;
        FieldType = fieldType;
        ValueType = valueType;
        NonNullableValueType = nonNullableValueType;
        ElementType = elementType;
        ContainerKind = containerKind;
        Codec = codec;
        IsReactive = isReactive;
        IsNullable = isNullable;
        DuckDbColumnType = BuildColumnType();
        _getProjectValue = FieldAccessorFactory.BuildGetProjectValue(field, isReactive);
        _getFieldStorage = FieldAccessorFactory.BuildGetFieldStorage(field);
        _setProjectValue = FieldAccessorFactory.BuildSetProjectValue(field, isReactive);
        _setFieldStorage = FieldAccessorFactory.BuildSetFieldStorage(field);
    }

    public static FieldDescriptor TryCreate(ComponentDescriptor component, FieldInfo field)
    {
        var attr = field.GetCustomAttribute<ProjectFieldAttribute>();
        if (attr == null)
            return null;

        var fieldType = field.FieldType;
        var isReactive = IsReactiveProperty(fieldType);
        var valueType = isReactive ? fieldType.GetGenericArguments()[0] : fieldType;
        var nonNullableType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        var isNullable = !valueType.IsValueType || Nullable.GetUnderlyingType(valueType) != null;

        var (shape, elementType, containerKind, codec) = ResolveShape(attr.StorageKind, nonNullableType);

        return new FieldDescriptor(
            component,
            field,
            attr,
            shape,
            fieldType,
            valueType,
            nonNullableType,
            elementType,
            containerKind,
            codec,
            isReactive,
            isNullable);
    }

    #region Value access (reactive unwrap)

    public object GetProjectValue(object component) => _getProjectValue(component);

    public object GetFieldStorageObject(object component) => _getFieldStorage(component);

    public void SetProjectValue(object component, object value) => _setProjectValue(component, value);

    public void SetFieldStorageObject(object component, object value) => _setFieldStorage(component, value);

    #endregion

    #region Shape resolution

    private static (FieldShape, Type, ContainerKind, StructCodec) ResolveShape(StorageKind storageKind, Type valueType)
    {
        switch (storageKind)
        {
            case StorageKind.Entity:
                return ResolveEntityShape(valueType);
            case StorageKind.Blob:
                return (FieldShape.Blob, null, ContainerKind.None, null);
            case StorageKind.Auto:
                return ResolveAutoShape(valueType);
            default:
                throw new ArgumentOutOfRangeException(nameof(storageKind), storageKind, null);
        }
    }

    private static (FieldShape, Type, ContainerKind, StructCodec) ResolveAutoShape(Type valueType)
    {
        if (IsScalar(valueType))
            return (FieldShape.Scalar, null, ContainerKind.None, null);

        if (StructCodecRegistry.TryGet(valueType, out var structCodec))
            return (FieldShape.Struct, null, ContainerKind.None, structCodec);

        if (TryResolveArray(valueType, out var elementType, out var containerKind))
        {
            if (StructCodecRegistry.TryGet(elementType, out var elementCodec))
                return (FieldShape.StructArray, elementType, containerKind, elementCodec);
            if (IsScalar(elementType))
                return (FieldShape.PrimitiveArray, elementType, containerKind, null);
        }

        throw new InvalidOperationException(
            $"{valueType} has no structured DuckDB mapping. Mark the field with StorageKind.Blob if it must stay binary.");
    }

    private static (FieldShape, Type, ContainerKind, StructCodec) ResolveEntityShape(Type valueType)
    {
        if (valueType == typeof(Entity))
            return (FieldShape.EntityRef, null, ContainerKind.None, null);

        if (!valueType.IsGenericType)
            throw new InvalidOperationException($"{valueType} is not a supported entity field.");

        var def = valueType.GetGenericTypeDefinition();
        var args = valueType.GetGenericArguments();

        if (def == typeof(List<>) && args[0] == typeof(Entity))
            return (FieldShape.EntityArray, typeof(Entity), ContainerKind.List, null);
        if (def == typeof(ObservableList<>) && args[0] == typeof(Entity))
            return (FieldShape.EntityArray, typeof(Entity), ContainerKind.ObservableList, null);
        if (def == typeof(HashSet<>) && args[0] == typeof(Entity))
            return (FieldShape.EntityArray, typeof(Entity), ContainerKind.HashSet, null);
        if (def == typeof(ObservableHashSet<>) && args[0] == typeof(Entity))
            return (FieldShape.EntityArray, typeof(Entity), ContainerKind.ObservableHashSet, null);
        if ((def == typeof(SortedList<,>) || def == typeof(ObservableSortedList<,>)) &&
            args[0] == typeof(int) && args[1] == typeof(Entity))
            return (FieldShape.EntityMap, typeof(Entity), ContainerKind.None, null);

        throw new InvalidOperationException($"{valueType} is not a supported entity field.");
    }

    private static bool TryResolveArray(Type valueType, out Type elementType, out ContainerKind containerKind)
    {
        elementType = null;
        containerKind = ContainerKind.None;

        if (valueType.IsArray)
        {
            elementType = valueType.GetElementType();
            containerKind = ContainerKind.Array;
            return true;
        }

        if (!valueType.IsGenericType)
            return false;

        var def = valueType.GetGenericTypeDefinition();
        if (def == typeof(ImmutableArray<>)) { elementType = valueType.GetGenericArguments()[0]; containerKind = ContainerKind.ImmutableArray; return true; }
        if (def == typeof(List<>)) { elementType = valueType.GetGenericArguments()[0]; containerKind = ContainerKind.List; return true; }
        if (def == typeof(ObservableList<>)) { elementType = valueType.GetGenericArguments()[0]; containerKind = ContainerKind.ObservableList; return true; }

        return false;
    }

    #endregion

    #region DuckDB type mapping

    private string BuildColumnType()
    {
        return Shape switch
        {
            FieldShape.Scalar => DuckScalarType(NonNullableValueType),
            FieldShape.Struct => Codec.DuckDbType,
            FieldShape.StructArray => Codec.DuckDbType + "[]",
            FieldShape.PrimitiveArray => DuckScalarType(ElementType) + "[]",
            FieldShape.EntityRef => "INTEGER",
            FieldShape.EntityArray => "INTEGER[]",
            FieldShape.EntityMap => "MAP(INTEGER, INTEGER)",
            FieldShape.Blob => "BLOB",
            _ => throw new ArgumentOutOfRangeException(nameof(Shape), Shape, null)
        };
    }

    public static string DuckScalarType(Type type)
    {
        if (type.IsEnum)
            return "INTEGER";
        if (type == typeof(Guid)) return "UUID";
        if (type == typeof(string)) return "VARCHAR";
        if (type == typeof(bool)) return "BOOLEAN";
        if (type == typeof(byte)) return "UTINYINT";
        if (type == typeof(sbyte)) return "TINYINT";
        if (type == typeof(short)) return "SMALLINT";
        if (type == typeof(ushort)) return "USMALLINT";
        if (type == typeof(int)) return "INTEGER";
        if (type == typeof(uint)) return "UINTEGER";
        if (type == typeof(long)) return "BIGINT";
        if (type == typeof(ulong)) return "UBIGINT";
        if (type == typeof(float)) return "FLOAT";
        if (type == typeof(double)) return "DOUBLE";
        throw new InvalidOperationException($"{type} has no DuckDB scalar type.");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Normalize one STRUCT row DuckDB returns (usually a Dictionary) to a leaf-name lookup.
    /// Used by StructCodec to read leaves without boxing the composed element.
    /// </summary>
    public static IReadOnlyDictionary<string, object> AsStructDict(object structValue)
    {
        if (structValue is IReadOnlyDictionary<string, object> readOnly)
            return readOnly;
        if (structValue is IDictionary<string, object> dict)
            return new Dictionary<string, object>(dict);

        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in (IDictionary)structValue)
            result[Convert.ToString(entry.Key)!] = entry.Value;
        return result;
    }

    /// <summary>Enumerate an array/list value as boxed elements, treating a default ImmutableArray as empty.</summary>
    public static IEnumerable<object> EnumerateArray(object value)
    {
        if (value == null)
            yield break;

        var type = value.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            var isDefault = (bool)type.GetProperty("IsDefault")!.GetValue(value)!;
            if (isDefault)
                yield break;
        }

        foreach (var item in (IEnumerable)value)
            yield return item;
    }

    private static bool IsReactiveProperty(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReactiveProperty<>);

    private static bool IsScalar(Type type)
    {
        return type == typeof(string)
               || type == typeof(Guid)
               || type == typeof(bool)
               || type.IsEnum
               || type == typeof(byte)
               || type == typeof(sbyte)
               || type == typeof(short)
               || type == typeof(ushort)
               || type == typeof(int)
               || type == typeof(uint)
               || type == typeof(long)
               || type == typeof(ulong)
               || type == typeof(float)
               || type == typeof(double);
    }

    #endregion
}
