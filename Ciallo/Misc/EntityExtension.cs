using Ciallo.Data;

namespace Massive;

public static class EntityExtension
{
    public static void Set<TWrapper>(this Entity self, Entity e) where TWrapper : EntityWrapper, new()
    {
        var wrapper = new TWrapper
        {
            Value = e
        };
        self.Set(wrapper);
    }

    public static bool IsNull(this Entity self) => self.World == null;
    public static bool IsNotNull(this Entity self) => self.World != null;
}