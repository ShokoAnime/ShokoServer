using System;
using System.Collections.Generic;

namespace Shoko.Server.Scheduling.Concurrency;

public struct JobTypes
{
    public JobTypes(IEnumerable<Type> TypesToExclude, IDictionary<Type, int> TypesToLimit, IEnumerable<IEnumerable<Type>> AvailableConcurrencyGroups)
    {
        this.TypesToExclude = TypesToExclude;
        this.TypesToLimit = TypesToLimit;
        this.AvailableConcurrencyGroups = AvailableConcurrencyGroups;
    }

    public JobTypes() : this(Array.Empty<Type>(), new Dictionary<Type, int>(), Array.Empty<Type[]>()) { }

    public IEnumerable<Type> TypesToExclude { get; init; }
    public IDictionary<Type, int> TypesToLimit { get; init; }
    public IEnumerable<IEnumerable<Type>> AvailableConcurrencyGroups { get; init; }
}
