using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// Scans all registered <see cref="IQueueJob"/> types and builds <see cref="WorkerPool"/>
/// instances automatically from concurrency attributes.
/// <para>
/// Pool assignment rules (in priority order):
/// <list type="number">
///   <item>
///     <c>[DisallowConcurrencyGroup("X")] + [LimitConcurrency(N)]</c>
///     → pool named <c>"X"</c>, max workers = N
///   </item>
///   <item>
///     <c>[DisallowConcurrencyGroup("X")]</c> only
///     → pool named <c>"X"</c>, max workers = 1 (mutual exclusion default)
///   </item>
///   <item>
///     <c>[LimitConcurrency(N)]</c> only (no group)
///     → pool named after the type, max workers = N
///   </item>
///   <item>
///     No concurrency attributes → assigned to the <c>"Default"</c> pool
///   </item>
/// </list>
/// </para>
/// Acquisition filters are linked automatically: each <see cref="IAcquisitionFilter"/> with a
/// non-null <see cref="IAcquisitionFilter.WatchedAttributeType"/> is attached to the pool that
/// handles types carrying that attribute.
/// </summary>
public class PoolDiscovery
{
    private readonly ILogger<PoolDiscovery> _logger;
    private readonly int _maxTotalWorkers;
    private readonly int _defaultPoolMaxWorkers;
    private readonly IDictionary<string, int> _overrides;

    public PoolDiscovery(
        ILogger<PoolDiscovery> logger,
        int maxTotalWorkers,
        int defaultPoolMaxWorkers,
        IDictionary<string, int>? overrides = null)
    {
        _logger = logger;
        _maxTotalWorkers = maxTotalWorkers;
        _defaultPoolMaxWorkers = defaultPoolMaxWorkers;
        _overrides = overrides ?? new Dictionary<string, int>();
    }

    /// <summary>
    /// Discovers pools from <paramref name="jobTypes"/> and wires up acquisition filters.
    /// </summary>
    public IReadOnlyList<WorkerPool> Discover(
        IEnumerable<Type> jobTypes,
        IEnumerable<IAcquisitionFilter> acquisitionFilters)
    {
        var filters = acquisitionFilters.ToList();

        // Group jobs by their pool name
        var poolGroups = new Dictionary<string, (List<Type> Types, int MaxWorkers)>(StringComparer.Ordinal);

        foreach (var type in jobTypes)
        {
            var groupAttr = type.GetCustomAttribute<DisallowConcurrencyGroupAttribute>();
            var limitAttr = type.GetCustomAttribute<LimitConcurrencyAttribute>();
            var disallowAttr = type.GetCustomAttribute<DisallowConcurrentExecutionAttribute>();

            var limit = disallowAttr != null ? 1 : limitAttr?.MaxConcurrentJobs ?? 0;

            // Apply override
            if (_overrides.TryGetValue(type.Name, out var ov) && ov > 0)
            {
                var maxAllowed = limitAttr?.MaxAllowedConcurrentJobs ?? limit;
                limit = maxAllowed > 0 ? Math.Min(maxAllowed, ov) : ov;
            }

            string poolName;
            int poolWorkers;

            if (groupAttr != null)
            {
                poolName = groupAttr.Group;
                // [DisallowConcurrencyGroup] without [LimitConcurrency] defaults to 1 worker
                // (mutual exclusion — only one job from the group runs at a time)
                poolWorkers = limit > 0 ? limit : 1;
            }
            else if (limit > 0)
            {
                poolName = type.Name;
                poolWorkers = limit;
            }
            else
            {
                poolName = "Default";
                poolWorkers = _defaultPoolMaxWorkers;
            }

            if (!poolGroups.TryGetValue(poolName, out var entry))
                poolGroups[poolName] = (new List<Type> { type }, poolWorkers);
            else
            {
                entry.Types.Add(type);
                // Pool size = minimum limit across all types in the group
                poolGroups[poolName] = (entry.Types, Math.Min(entry.MaxWorkers, poolWorkers));
            }
        }

        // Build WorkerPool instances with attached acquisition filters
        var pools = new List<WorkerPool>();
        foreach (var kv in poolGroups)
        {
            var poolName = kv.Key;
            var (types, maxWorkers) = kv.Value;
            var typeSet = new HashSet<Type>(types);

            // Pool priority = minimum WorkerPriority across all types in the pool.
            // Types with no AcquisitionAttribute get LowestPriority.
            var poolPriority = types.Count == 0
                ? AcquisitionAttribute.LowestPriority
                : types.Min(t =>
                    t.GetCustomAttributes<AcquisitionAttribute>(inherit: true)
                        .Select(a => a.WorkerPriority)
                        .DefaultIfEmpty(AcquisitionAttribute.LowestPriority)
                        .Min());

            // Attach filters whose watched attribute is present on any type in this pool
            var poolFilters = filters
                .Where(f =>
                {
                    var watchedAttr = f.WatchedAttributeType;
                    if (watchedAttr == null)
                    {
                        // Global filters (no WatchedAttributeType) attach to the Default pool only
                        return poolName == "Default";
                    }
                    // IsInstanceOfType matches the exact attribute type AND any subclass,
                    // so filters with a base-class WatchedAttributeType attach to pools that
                    // contain types decorated with derived attributes too.
                    return types.Any(t => t.GetCustomAttributes(inherit: true)
                        .Any(a => watchedAttr.IsInstanceOfType(a)));
                })
                .ToList();

            var pool = new WorkerPool(poolName, Math.Min(maxWorkers, _maxTotalWorkers), poolPriority, types, poolFilters);
            pools.Add(pool);

            _logger.LogInformation("Pool '{Name}': {TypeCount} types, {Workers} workers, {FilterCount} filters",
                poolName, types.Count, pool.MaxWorkers, poolFilters.Count);
        }

        // Ensure the Default pool always exists
        if (!poolGroups.ContainsKey("Default"))
        {
            var defaultWorkers = Math.Min(_defaultPoolMaxWorkers, _maxTotalWorkers);
            var defaultPool = new WorkerPool("Default", defaultWorkers,
                AcquisitionAttribute.LowestPriority,
                Array.Empty<Type>(),
                filters.Where(f => f.WatchedAttributeType == null).ToList());
            pools.Add(defaultPool);
            _logger.LogInformation("Pool 'Default': 0 explicit types, {Workers} workers (catch-all)", defaultWorkers);
        }

        return pools;
    }
}
