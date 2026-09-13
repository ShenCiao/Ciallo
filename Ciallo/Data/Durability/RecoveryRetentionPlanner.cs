using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciallo.Data;

internal readonly record struct RecoveryRetentionLimits(
    int PerDocumentLimit,
    int AccountFileLimit,
    long AccountByteLimit);

internal readonly record struct RecoveryRetentionPlan<T>(
    IReadOnlyList<T> Retained,
    IReadOnlyList<T> Evicted,
    bool LimitExceeded);

internal static class RecoveryRetentionPlanner
{
    /// <summary>
    /// Selects which recovery snapshots to retain and which to evict under the shared retention
    /// policy: newest-first within each document up to a per-document limit, then account-wide file
    /// and byte limits, always keeping at least the single newest snapshot. The plan is caller-agnostic;
    /// local and cloud reconciliation execute the resulting eviction list against their own storage.
    /// </summary>
    public static RecoveryRetentionPlan<T> Plan<T>(
        IEnumerable<T> items,
        Func<T, Guid> revisionId,
        Func<T, Guid> documentId,
        Func<T, DateTimeOffset> capturedAtUtc,
        Func<T, long> byteLength,
        RecoveryRetentionLimits limits)
    {
        // Stable ordering across storage backends: newest first, then descending RevisionId to break
        // ties when several snapshots share a capture instant. Local file enumeration and the Steam
        // file listing return items in different orders, so a deterministic key keeps both sides
        // evicting the same revisions.
        var ordered = items
            .OrderByDescending(capturedAtUtc)
            .ThenByDescending(revisionId)
            .ToList();

        var evictedIds = new HashSet<Guid>();
        var perDocumentLimit = Math.Max(1, limits.PerDocumentLimit);
        foreach (var group in ordered.GroupBy(documentId))
        {
            foreach (var item in group.Skip(perDocumentLimit))
                evictedIds.Add(revisionId(item));
        }

        var retained = ordered.Where(item => !evictedIds.Contains(revisionId(item))).ToList();
        var retainedBytes = retained.Sum(byteLength);
        while (retained.Count > 1 &&
               (retained.Count > limits.AccountFileLimit || retainedBytes > limits.AccountByteLimit))
        {
            var oldest = retained[^1];
            retained.RemoveAt(retained.Count - 1);
            retainedBytes -= byteLength(oldest);
            evictedIds.Add(revisionId(oldest));
        }

        var limitExceeded = retained.Count > limits.AccountFileLimit ||
                            retainedBytes > limits.AccountByteLimit;
        var evicted = ordered.Where(item => evictedIds.Contains(revisionId(item))).ToList();
        return new RecoveryRetentionPlan<T>(retained, evicted, limitExceeded);
    }
}
