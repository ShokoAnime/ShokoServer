#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// Startup-built, read-only view of per-type and per-group concurrency limits.
/// Built by <see cref="PoolDiscovery"/> before any workers start.
/// </summary>
public class ConcurrencyRegistry
{
    // Per-type max concurrency
    private readonly IReadOnlyDictionary<Type, int> _typeLimits;

    // Maps each job type to its concurrency group name (null if no group)
    private readonly IReadOnlyDictionary<Type, string?> _typeGroups;

    // Per-group max concurrency (pool size)
    private readonly IReadOnlyDictionary<string, int> _groupLimits;

    public ConcurrencyRegistry(
        IReadOnlyDictionary<Type, int> typeLimits,
        IReadOnlyDictionary<Type, string?> typeGroups,
        IReadOnlyDictionary<string, int> groupLimits)
    {
        _typeLimits = typeLimits;
        _typeGroups = typeGroups;
        _groupLimits = groupLimits;
    }

    /// <summary>Returns the per-type concurrency limit, or <c>int.MaxValue</c> if unlimited.</summary>
    public int GetTypeLimit(Type jobType) =>
        _typeLimits.TryGetValue(jobType, out var limit) ? limit : int.MaxValue;

    /// <summary>Returns the concurrency group name for <paramref name="jobType"/>, or <c>null</c>.</summary>
    public string? GetGroup(Type jobType) =>
        _typeGroups.TryGetValue(jobType, out var group) ? group : null;

    /// <summary>Returns the pool worker count for <paramref name="group"/>, or <c>int.MaxValue</c>.</summary>
    public int GetGroupLimit(string group) =>
        _groupLimits.TryGetValue(group, out var limit) ? limit : int.MaxValue;

    /// <summary>
    /// Returns true if <paramref name="jobType"/> may start executing, given the current
    /// per-type and per-group running counts.
    /// </summary>
    public bool CanRun(
        Type jobType,
        IReadOnlyDictionary<Type, int> typeRunningCounts,
        IReadOnlyDictionary<string, int> groupRunningCounts)
    {
        // Per-type limit
        var typeLimit = GetTypeLimit(jobType);
        typeRunningCounts.TryGetValue(jobType, out var typeRunning);
        if (typeRunning >= typeLimit) return false;

        // Per-group limit
        var group = GetGroup(jobType);
        if (group != null)
        {
            var groupLimit = GetGroupLimit(group);
            groupRunningCounts.TryGetValue(group, out var groupRunning);
            if (groupRunning >= groupLimit) return false;
        }

        return true;
    }

    /// <summary>
    /// Builds a <see cref="ConcurrencyRegistry"/> from a set of registered job types,
    /// applying <paramref name="overrides"/> to lower (but not raise) individual type limits.
    /// </summary>
    public static ConcurrencyRegistry Build(
        IEnumerable<Type> jobTypes,
        IDictionary<string, int>? overrides = null,
        int globalMax = int.MaxValue)
    {
        var typeLimits = new Dictionary<Type, int>();
        var typeGroups = new Dictionary<Type, string?>();
        var groupLimits = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var type in jobTypes)
        {
            var limitAttr = type.GetCustomAttribute<LimitConcurrencyAttribute>();
            var groupAttr = type.GetCustomAttribute<DisallowConcurrencyGroupAttribute>();
            var disallowAttr = type.GetCustomAttribute<DisallowConcurrentExecutionAttribute>();

            var limit = disallowAttr != null ? 1
                : limitAttr?.MaxConcurrentJobs ?? 0;

            var group = groupAttr?.Group;
            typeGroups[type] = group;

            if (limit > 0)
            {
                // Apply override (lower only)
                if (overrides != null && overrides.TryGetValue(type.Name, out var overrideVal) && overrideVal > 0)
                {
                    var maxAllowed = limitAttr?.MaxAllowedConcurrentJobs ?? limit;
                    limit = Math.Min(maxAllowed, Math.Max(1, overrideVal));
                }

                typeLimits[type] = Math.Min(limit, globalMax);
            }

            if (group != null && limit > 0)
            {
                // Group limit = minimum of all per-type limits in the group
                if (!groupLimits.TryGetValue(group, out var existing) || limit < existing)
                    groupLimits[group] = Math.Min(limit, globalMax);
            }
        }

        return new ConcurrencyRegistry(typeLimits, typeGroups, groupLimits);
    }
}
