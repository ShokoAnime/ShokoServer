#nullable enable
using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor;

/// <summary>
/// Singleton that carries the set of concrete <see cref="Abstractions.IQueueJob"/> types
/// discovered at startup. Injected into components that need the type list without
/// instantiating job objects via DI (which would cause circular dependencies).
/// </summary>
public class QueueJobTypeRegistry
{
    public IReadOnlyList<Type> JobTypes { get; }

    public QueueJobTypeRegistry(IReadOnlyList<Type> jobTypes)
    {
        JobTypes = jobTypes;
    }
}
