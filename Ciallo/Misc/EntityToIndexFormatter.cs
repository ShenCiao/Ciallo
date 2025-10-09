using System.Collections.Generic;
using System.Linq;
using Massive;
using MessagePack;
using MessagePack.Formatters;

namespace Ciallo.Misc;

public class EntityToIndexFormatter : IMessagePackFormatter<Entity>
{
    public static readonly EntityToIndexFormatter Instance = new();
    private Dictionary<Entity, int> _entityToIndex = [];
    private List<Entity> _indexToEntity = [];
    
    public List<Entity> EntityList
    {
        get => _indexToEntity;
        set
        {
            _indexToEntity = value;
            _entityToIndex = _indexToEntity
                .Select((entity, index) => new { entity, index })
                .ToDictionary(x => x.entity, x => x.index);
        }
    }
    
    public void Serialize(ref MessagePackWriter writer, Entity value, MessagePackSerializerOptions options)
    {
        var i = _entityToIndex.GetValueOrDefault(value, -1);
        MessagePackSerializer.Serialize(ref writer, i, options);
    }

    public Entity Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        int index = MessagePackSerializer.Deserialize<int>(ref reader, options);
        
        return index < 0 || index >= _indexToEntity.Count
            ? new Entity()
            : _indexToEntity[index];
    }
}