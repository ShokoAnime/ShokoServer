using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor;

/// <summary>
/// Singleton that carries the set of concrete <see cref="Abstractions.IQueueJob"/> types
/// discovered at startup. Injected into components that need the type list without
/// instantiating job objects via DI (which would cause circular dependencies).
///
/// <para>
/// The registry behaves as a mutable builder until its <see cref="JobTypes"/> list is read
/// for the first time, at which point it freezes. Plugins append their job types during
/// service registration (see <c>QueueProcessorExtensions.AddQueueJobsFromAssembly</c>);
/// the first read happens later, when the worker pool starts as a hosted service.
/// </para>
/// </summary>
public class QueueJobTypeRegistry
{
    private readonly List<Type> _jobTypes = new();
    private readonly object _lock = new();
    private volatile bool _frozen;

    /// <summary>Snapshot of all registered job types. Reading this property freezes the registry.</summary>
    public IReadOnlyList<Type> JobTypes
    {
        get
        {
            lock (_lock)
            {
                _frozen = true;
                return _jobTypes.ToArray();
            }
        }
    }

    /// <summary>
    /// Append job types to the registry. Throws <see cref="InvalidOperationException"/> if
    /// called after <see cref="JobTypes"/> has been read.
    /// </summary>
    public void Add(IEnumerable<Type> types)
    {
        lock (_lock)
        {
            if (_frozen)
                throw new InvalidOperationException(
                    $"{nameof(QueueJobTypeRegistry)} is frozen. Add job types during service registration, before the queue starts.");
            _jobTypes.AddRange(types);
        }
    }
}
