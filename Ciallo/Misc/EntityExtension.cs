using System;
using System.Runtime.CompilerServices;
using Ciallo.Data;
using Frent;

namespace Ciallo;

public static class EntityExtension
{
    extension(Entity self)
    {
        /// <summary>
        /// Check if entity has been deleted by user. It may or may not has been deleted by undo stack.
        /// </summary>
        public bool IsDyingOrDead => !self.IsAlive || !self.Tagged<ToSerializeTag>();

        public bool IsDocument => self.World.Document() == self; // If entity is the singleton document entity.
        public bool IsCelFolder => self.TryGet<FolderLayerSetting>()?.IsCelFolder == true;
        public Entity Document => self.World.Document();
        public long PackedValue => Unsafe.As<Entity, long>(ref Unsafe.AsRef(in self));

        /// <summary>
        /// Return null if entity is null or do not have T component.
        /// </summary>
        public T TryGet<T>() where T : class => self.IsNull || !self.TryHas<T>() ? null : self.Get<T>();
        public T GetRemove<T>() where T : class
        {
            var component = self.Get<T>();
            self.Remove<T>();
            return component;
        }
        public T TryGetRemove<T>() where T : class => self.IsNull || !self.TryHas<T>() ? null : self.GetRemove<T>();

        public static bool IsNotNull(Entity e) => !e.IsNull;
    }

    extension(long packedValue)
    {
        public Entity ToEntity() => Unsafe.As<long, Entity>(ref Unsafe.AsRef(in packedValue));
    }

    public static T AddTo<T>(this T disposable, Entity e) where T : IDisposable
    {
        e.OnDelete += _ => disposable.Dispose();
        return disposable;
    }
}
