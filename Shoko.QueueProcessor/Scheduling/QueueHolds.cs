using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Shoko.QueueProcessor.Scheduling;

/// <summary>
///   The pools and job types held by hand: their jobs wait in the queue,
///   shown as blocked, until they are released. Jobs already running finish.
/// </summary>
/// <remarks>
///   Pools are named as <see cref="Analytics.PoolStatus.Name"/> names them,
///   ignoring case, and job types by <see cref="Builder.JobTypeNames.Key"/>.
///   Until the queue has found its pools, any name is taken. Once it has, a
///   name it does not know cannot be held, but can still be released, and
///   the holds restored at start keep the names they had, such as those of a
///   plugin that is not loaded right now.
/// </remarks>
public sealed class QueueHolds
{
    private readonly Lock _lock = new();

    private HashSet<string> _pools = new(StringComparer.OrdinalIgnoreCase);

    private HashSet<string> _jobTypes = new(StringComparer.Ordinal);

    private HashSet<string>? _knownPools;

    private HashSet<string>? _knownJobTypes;

    /// <summary>
    ///   Raised after the holds change.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    ///   The pools held.
    /// </summary>
    public IReadOnlySet<string> HeldPools
    {
        get
        {
            lock (_lock)
                return new HashSet<string>(_pools, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///   The job types held, by their keys.
    /// </summary>
    public IReadOnlySet<string> HeldJobTypes
    {
        get
        {
            lock (_lock)
                return new HashSet<string>(_jobTypes, StringComparer.Ordinal);
        }
    }

    /// <summary>
    ///   Holds or releases a pool.
    /// </summary>
    /// <param name="poolName">The pool.</param>
    /// <param name="held">Whether to hold it.</param>
    /// <returns><c>true</c> if the holds changed.</returns>
    public bool SetPoolHeld(string poolName, bool held)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        bool changed;
        lock (_lock)
        {
            if (held && _knownPools is not null && !_knownPools.Contains(poolName))
                throw new ArgumentException($"The queue has no pool named \"{poolName}\".", nameof(poolName));
            changed = held ? _pools.Add(poolName) : _pools.Remove(poolName);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
        return changed;
    }

    /// <summary>
    ///   Holds or releases a job type.
    /// </summary>
    /// <param name="jobTypeKey">The job type's key.</param>
    /// <param name="held">Whether to hold it.</param>
    /// <returns><c>true</c> if the holds changed.</returns>
    public bool SetJobTypeHeld(string jobTypeKey, bool held)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobTypeKey);
        bool changed;
        lock (_lock)
        {
            if (held && _knownJobTypes is not null && !_knownJobTypes.Contains(jobTypeKey))
                throw new ArgumentException($"The queue has no job type \"{jobTypeKey}\".", nameof(jobTypeKey));
            changed = held ? _jobTypes.Add(jobTypeKey) : _jobTypes.Remove(jobTypeKey);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
        return changed;
    }

    /// <summary>
    ///   Replaces every hold, such as with the ones saved before a restart.
    ///   Takes any name, known to the queue or not.
    /// </summary>
    /// <param name="poolNames">The pools to hold.</param>
    /// <param name="jobTypeKeys">The job types to hold, by their keys.</param>
    public void Set(IEnumerable<string> poolNames, IEnumerable<string> jobTypeKeys)
    {
        ArgumentNullException.ThrowIfNull(poolNames);
        ArgumentNullException.ThrowIfNull(jobTypeKeys);
        var pools = new HashSet<string>(poolNames.Where(name => !string.IsNullOrWhiteSpace(name)), StringComparer.OrdinalIgnoreCase);
        var jobTypes = new HashSet<string>(jobTypeKeys.Where(key => !string.IsNullOrWhiteSpace(key)), StringComparer.Ordinal);
        bool changed;
        lock (_lock)
        {
            changed = !_pools.SetEquals(pools) || !_jobTypes.SetEquals(jobTypes);
            (_pools, _jobTypes) = (pools, jobTypes);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///   Whether a pool can be held: the queue has it, or has not found its
    ///   pools yet.
    /// </summary>
    /// <param name="poolName">The pool's name, ignoring case.</param>
    /// <returns><c>true</c> if it can be held.</returns>
    public bool IsKnownPool(string poolName)
    {
        lock (_lock)
            return _knownPools is null || _knownPools.Contains(poolName);
    }

    /// <summary>
    ///   Whether a job type can be held: the queue has it, or has not found
    ///   its pools yet.
    /// </summary>
    /// <param name="jobTypeKey">The job type's <see cref="Builder.JobTypeNames.Key"/>.</param>
    /// <returns><c>true</c> if it can be held.</returns>
    public bool IsKnownJobType(string jobTypeKey)
    {
        lock (_lock)
            return _knownJobTypes is null || _knownJobTypes.Contains(jobTypeKey);
    }

    /// <summary>
    ///   Tells the holds which pools and job types the queue has, so a name it
    ///   does not know can no longer be held.
    /// </summary>
    /// <param name="poolNames">The names of the pools.</param>
    /// <param name="jobTypeKeys">The keys of the job types.</param>
    /// <returns>The names held now that the queue does not know.</returns>
    internal (IReadOnlyList<string> Pools, IReadOnlyList<string> JobTypes) SetKnown(IEnumerable<string> poolNames, IEnumerable<string> jobTypeKeys)
    {
        ArgumentNullException.ThrowIfNull(poolNames);
        ArgumentNullException.ThrowIfNull(jobTypeKeys);
        lock (_lock)
        {
            _knownPools = new(poolNames, StringComparer.OrdinalIgnoreCase);
            _knownJobTypes = new(jobTypeKeys, StringComparer.Ordinal);
            return ([.. _pools.Where(name => !_knownPools.Contains(name))], [.. _jobTypes.Where(key => !_knownJobTypes.Contains(key))]);
        }
    }
}
