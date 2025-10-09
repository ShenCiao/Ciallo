using System.Runtime.Serialization;
using Massive;

namespace Ciallo.Data;

[DataContract, ToSerialize] public class StrokeBrush : EntityWrapper;

[DataContract]
public class EntityWrapper
{
    [DataMember] public Entity Value;

    public EntityWrapper(Entity value = default)
    {
        Value = value;
    }

    public static implicit operator Entity(EntityWrapper wrapper) => wrapper.Value;

    // public void Add<T>() where T : new() => Value.Add<T>();
    // public void Add<T>(in T component) => Value.Add(component);
    // public void Set<T>() where T : new() => Value.Set<T>();
    // public void Set<T>(in T component) => Value.Set(component);
    // public void Remove<T>() => Value.Remove<T>();
    public bool Has<T>() => Value.Has<T>();
    public T Get<T>() => Value.Get<T>();
}