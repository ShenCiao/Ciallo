using System;

namespace Ciallo.Command;

public enum HistorySequenceKind
{
    LayerVisibility,
    TimelineInteraction,
}

internal static class HistorySequenceKindExtensions
{
    public static string GetActionName(this HistorySequenceKind kind) => kind switch
    {
        HistorySequenceKind.LayerVisibility => "Layer Visibility",
        HistorySequenceKind.TimelineInteraction => "Timeline Interaction",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
