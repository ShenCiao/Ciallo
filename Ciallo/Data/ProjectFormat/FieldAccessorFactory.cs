using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Ciallo.Data;

/// <summary>
/// Compiles per-field get/set delegates once at startup so persistence save (Capture) and load
/// (DeserializeField) never call FieldInfo/PropertyInfo reflection per component instance.
///
/// Public fields use expression-tree Compile(); the two private [ProjectField] fields
/// (EntityTreeNode._parent, _children) use DynamicMethod(skipVisibility: true) because
/// Expression.Field cannot bypass a private field's accessibility check under Compile().
///
/// A reactive field is a ReactiveProperty&lt;T&gt; container. "Project value" get/set unwraps the
/// container's Value; "field storage" get/set touches the container object itself.
/// </summary>
internal static class FieldAccessorFactory
{
    // component -> unwrapped value (reactive: field.Value, with null-container short-circuit)
    public static Func<object, object> BuildGetProjectValue(FieldInfo field, bool isReactive)
    {
        var storageGetter = BuildGetFieldStorage(field);
        if (!isReactive)
            return storageGetter;

        var valueProperty = field.FieldType.GetProperty("Value")
                            ?? throw new InvalidOperationException($"{field.FieldType} has no Value property.");
        var valueGetter = BuildPropertyGetter(valueProperty);
        return component =>
        {
            var container = storageGetter(component);
            return container == null ? null : valueGetter(container);
        };
    }

    // component -> raw field object (the ReactiveProperty container for reactive fields)
    public static Func<object, object> BuildGetFieldStorage(FieldInfo field)
    {
        if (!field.IsPublic)
            return BuildDynamicGetter(field);

        var componentParam = Expression.Parameter(typeof(object), "component");
        var typedComponent = Expression.Convert(componentParam, field.DeclaringType!);
        var access = Expression.Field(typedComponent, field);
        var boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object>>(boxed, componentParam).Compile();
    }
    // component, value -> write unwrapped value (reactive: set container.Value, or construct a new
    // container when the field is null — matching the original SetProjectValue semantics)
    public static Action<object, object> BuildSetProjectValue(FieldInfo field, bool isReactive)
    {
        if (!isReactive)
            return BuildSetFieldStorage(field);

        var valueProperty = field.FieldType.GetProperty("Value")
                            ?? throw new InvalidOperationException($"{field.FieldType} has no Value property.");
        var storageGetter = BuildGetFieldStorage(field);
        var storageSetter = BuildSetFieldStorage(field);
        var valueSetter = BuildPropertySetter(valueProperty);
        var containerType = field.FieldType;
        return (component, value) =>
        {
            var container = storageGetter(component);
            if (container != null)
            {
                valueSetter(container, value);
                return;
            }
            storageSetter(component, Activator.CreateInstance(containerType, value));
        };
    }

    // component, object -> write the raw field (container for reactive fields)
    public static Action<object, object> BuildSetFieldStorage(FieldInfo field)
    {
        if (field.IsInitOnly || !field.IsPublic)
            return BuildDynamicSetter(field);

        var componentParam = Expression.Parameter(typeof(object), "component");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var typedComponent = Expression.Convert(componentParam, field.DeclaringType!);
        var typedValue = Expression.Convert(valueParam, field.FieldType);
        var assign = Expression.Assign(Expression.Field(typedComponent, field), typedValue);
        return Expression.Lambda<Action<object, object>>(assign, componentParam, valueParam).Compile();
    }

    // PrimitiveArray: when the CLR element type already equals its DuckDB list element type
    // (float/int/double/long/Guid), bind a boxing-free strongly-typed copy. Otherwise return null so
    // the caller uses the reflective ToDbList fallback (enum, and widening types like short/byte/ulong).
    public static Func<object, IList> BuildPrimitiveDbList(Type elementType)
    {
        if (elementType != ScalarConvert.DbListElementType(elementType))
            return null;

        var method = typeof(FieldAccessorFactory)
            .GetMethod(nameof(CopyToTypedList), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elementType);
        return (Func<object, IList>)method.CreateDelegate(typeof(Func<object, IList>));
    }

    private static IList CopyToTypedList<T>(object source) => ScalarConvert.CopyList((IEnumerable<T>)source);

    private static Func<object, object> BuildPropertyGetter(PropertyInfo property)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var typedInstance = Expression.Convert(instanceParam, property.DeclaringType!);
        var access = Expression.Property(typedInstance, property);
        var boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object>>(boxed, instanceParam).Compile();
    }

    private static Action<object, object> BuildPropertySetter(PropertyInfo property)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var typedInstance = Expression.Convert(instanceParam, property.DeclaringType!);
        var typedValue = Expression.Convert(valueParam, property.PropertyType);
        var assign = Expression.Assign(Expression.Property(typedInstance, property), typedValue);
        return Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam).Compile();
    }

    // Private / init-only fields: emit IL with visibility checks skipped. Expression.Compile()
    // enforces field accessibility, so it cannot read _parent/_children or assign an init-only field.
    private static Func<object, object> BuildDynamicGetter(FieldInfo field)
    {
        var method = new DynamicMethod(
            "get_" + field.DeclaringType!.Name + "_" + field.Name,
            typeof(object),
            [typeof(object)],
            field.DeclaringType,
            skipVisibility: true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, field.DeclaringType);
        il.Emit(OpCodes.Ldfld, field);
        if (field.FieldType.IsValueType)
            il.Emit(OpCodes.Box, field.FieldType);
        il.Emit(OpCodes.Ret);
        return (Func<object, object>)method.CreateDelegate(typeof(Func<object, object>));
    }

    private static Action<object, object> BuildDynamicSetter(FieldInfo field)
    {
        var method = new DynamicMethod(
            "set_" + field.DeclaringType!.Name + "_" + field.Name,
            typeof(void),
            [typeof(object), typeof(object)],
            field.DeclaringType,
            skipVisibility: true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, field.DeclaringType);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
        return (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>));
    }
}
