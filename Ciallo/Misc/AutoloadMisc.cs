using Godot;
using System.Runtime;
using MessagePack;
using MessagePackGodot;
using MessagePack.Resolvers;
using Ciallo.Diagnostics;

namespace Ciallo;

public partial class AutoloadMisc : Node
{
    public override void _EnterTree()
    {
        AppBugReport.InstallExceptionHandlers();
        // Hopefully this can reduce GC spikes.
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Message pack serializer setup
        var defaultResolver = CompositeResolver.Create(
            [
                EntityToIndexFormatter.Instance,
                TypeFormatter.Instance,
                ImageTextureFormatter.Instance,
                ImageFormatter.Instance
            ],
            [
                GodotResolver.Instance,
                AttributeFormatterResolver.Instance,
                ReactivePropertyResolver.Instance,
                StandardResolverAllowPrivate.Instance
            ]
        );
        MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(defaultResolver);

        // May 30, 2026. Stored Frent's ECS world in a SQLite database. The ECS model maps naturally to
        // relational storage at the schema level:
        // - Entity ids can be primary keys.
        // - Each component type can be a table.
        // - Component fields can be columns.
        // - Entity-valued fields can be foreign keys to the entity table.
        //
        // The main goal is not powerful relational querying. It is a file format that is
        // easy for other programs to inspect: they should quickly see which entities have
        // which components and which fields exist.
        //
        // June 22, 2026. Moved from SQLite-in-a-zip to a plain DuckDB file (.ciallo IS the db).
        // DuckDB's native STRUCT/LIST/MAP let creative data (Color, Vector2, Transform2D, Bezier
        // curves, stroke geometry) live as structured columns instead of opaque blobs, so the file
        // is genuinely inspectable via SQL. Only true binary media (textures) stays MessagePack.

    }

    public override void _Notification(int what) { }

    public override void _Ready() { }

    public override void _ExitTree()
    {
        AppBugReport.UninstallExceptionHandlers();
    }
}
