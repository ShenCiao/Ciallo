using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Frent;
using ObservableCollections;

namespace Ciallo.Data;

/// <summary>
/// Rebuilds the concrete collection type a field declares (ImmutableArray&lt;T&gt;, List&lt;T&gt;,
/// ObservableList&lt;T&gt;, HashSet&lt;T&gt;, ObservableHashSet&lt;T&gt;, T[]) from a sequence of
/// already-converted, correctly-typed elements.
/// </summary>
internal static class ContainerFactory
{
    private static readonly MethodInfo AsImmutableArrayMethod =
        typeof(ContainerFactory).GetMethod(nameof(AsImmutableArray), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static object Build(ContainerKind kind, Type elementType, IReadOnlyList<object> elements)
    {
        var typed = Array.CreateInstance(elementType, elements.Count);
        for (int i = 0; i < elements.Count; i++)
            typed.SetValue(elements[i], i);

        switch (kind)
        {
            case ContainerKind.Array:
                return typed;
            case ContainerKind.ImmutableArray:
                return AsImmutableArrayMethod.MakeGenericMethod(elementType).Invoke(null, [typed]);
            case ContainerKind.List:
            case ContainerKind.ObservableList:
            case ContainerKind.HashSet:
            case ContainerKind.ObservableHashSet:
                var collectionType = CollectionType(kind, elementType);
                var collection = Activator.CreateInstance(collectionType)!;
                AddRange(collection, typed);
                return collection;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>
    /// Populate an existing entity collection instance (used for readonly fields like the tree's
    /// children list, whose instance is created by the component constructor and cannot be reassigned).
    /// </summary>
    public static void PopulateEntityCollection(object collection, IReadOnlyList<Entity> entities)
    {
        switch (collection)
        {
            case ObservableList<Entity> observableList:
                observableList.Clear();
                observableList.AddRange(entities);
                return;
            case ObservableHashSet<Entity> observableSet:
                observableSet.Clear();
                observableSet.AddRange(entities);
                return;
            case ICollection<Entity> generic:
                generic.Clear();
                foreach (var e in entities)
                    generic.Add(e);
                return;
            default:
                throw new InvalidOperationException($"{collection.GetType()} is not a supported entity collection.");
        }
    }

    private static Type CollectionType(ContainerKind kind, Type elementType) => kind switch
    {
        ContainerKind.List => typeof(List<>).MakeGenericType(elementType),
        ContainerKind.ObservableList => typeof(ObservableList<>).MakeGenericType(elementType),
        ContainerKind.HashSet => typeof(HashSet<>).MakeGenericType(elementType),
        ContainerKind.ObservableHashSet => typeof(ObservableHashSet<>).MakeGenericType(elementType),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static void AddRange(object collection, Array typed)
    {
        // ICollection<T>.Add via the non-generic IList when available, else reflection.
        if (collection is IList list && collection.GetType().IsGenericType &&
            collection.GetType().GetGenericTypeDefinition() == typeof(List<>))
        {
            foreach (var item in typed)
                list.Add(item);
            return;
        }

        var add = collection.GetType().GetMethod("Add", [typed.GetType().GetElementType()!])
                  ?? throw new InvalidOperationException($"{collection.GetType()} has no Add method.");
        foreach (var item in typed)
            add.Invoke(collection, [item]);
    }

    private static ImmutableArray<T> AsImmutableArray<T>(T[] values) =>
        ImmutableCollectionsMarshal.AsImmutableArray(values);
}

/// <summary>Scalar value conversion between CLR field types and the boxed values DuckDB exchanges.</summary>
internal static class ScalarConvert
{
    /// <summary>Convert a CLR field value into the value to bind for a DuckDB scalar column.</summary>
    public static object ToDb(Type nonNullableType, object value)
    {
        if (value == null)
            return null;
        if (nonNullableType.IsEnum)
            return Convert.ToInt32(value);
        return value; // string/bool/int/long/float/double bind directly
    }

    /// <summary>Convert a boxed DuckDB value back into the CLR field type.</summary>
    public static object FromDb(Type nonNullableType, object db)
    {
        if (db is null or DBNull)
            return null;
        if (nonNullableType.IsEnum)
            return Enum.ToObject(nonNullableType, Convert.ToInt64(db));
        if (nonNullableType == typeof(Guid))
            return (Guid)db;
        if (nonNullableType == typeof(string)) return Convert.ToString(db);
        if (nonNullableType == typeof(bool)) return Convert.ToBoolean(db);
        if (nonNullableType == typeof(byte)) return Convert.ToByte(db);
        if (nonNullableType == typeof(sbyte)) return Convert.ToSByte(db);
        if (nonNullableType == typeof(short)) return Convert.ToInt16(db);
        if (nonNullableType == typeof(ushort)) return Convert.ToUInt16(db);
        if (nonNullableType == typeof(int)) return Convert.ToInt32(db);
        if (nonNullableType == typeof(uint)) return Convert.ToUInt32(db);
        if (nonNullableType == typeof(long)) return Convert.ToInt64(db);
        if (nonNullableType == typeof(ulong)) return Convert.ToUInt64(db);
        if (nonNullableType == typeof(float)) return Convert.ToSingle(db);
        if (nonNullableType == typeof(double)) return Convert.ToDouble(db);
        return Convert.ChangeType(db, nonNullableType);
    }

    /// <summary>
    /// Build the strongly-typed list DuckDB.NET expects when binding a scalar array parameter
    /// (e.g. <c>FLOAT[]</c> wants List&lt;float&gt;, <c>INTEGER[]</c> wants List&lt;int&gt;).
    /// Enums and small integers are stored as INTEGER, so they bind as List&lt;int&gt;.
    /// </summary>
    public static IList ToDbList(Type elementType, IEnumerable<object> elements)
    {
        if (elementType == typeof(Guid))
            return elements.Cast<Guid>().ToList();
        if (elementType == typeof(float))
            return elements.Select(Convert.ToSingle).ToList();
        if (elementType == typeof(double))
            return elements.Select(Convert.ToDouble).ToList();
        if (elementType == typeof(long) || elementType == typeof(ulong))
            return elements.Select(Convert.ToInt64).ToList();
        // int / short / byte / enum -> INTEGER
        return elements.Select(Convert.ToInt32).ToList();
    }
}
