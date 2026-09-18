using System;
using System.Reflection;
using Godot;
using Newtonsoft.Json;

namespace Ciallo;

public class ReactivePropertyConverter : JsonConverter
{
    public static readonly ReactivePropertyConverter Instance = new();

    public override bool CanConvert(Type objectType)
    {
        return objectType.IsGenericType &&
               objectType.GetGenericTypeDefinition().Name == "ReactiveProperty`1";
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            // By design, ReactiveProperty itself should not be null in any case 
            GD.PrintErr("ReactivePropertyConverter: value is null, writing null.");
            return;
        }
        var valueProperty = value.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        var innerValue = valueProperty!.GetValue(value);
        serializer.Serialize(writer, innerValue);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var valueType = objectType.GetGenericArguments()[0];
        var deserializedValue = serializer.Deserialize(reader, valueType);
        if (existingValue == null) return Activator.CreateInstance(objectType, deserializedValue);
        // set Value to deserializedValue
        var valueProperty = existingValue.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        valueProperty!.SetValue(existingValue, deserializedValue);
        return existingValue;
    }
}
