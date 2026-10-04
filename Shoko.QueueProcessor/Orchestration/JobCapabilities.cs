using System;
using System.Collections.Concurrent;
using System.Linq;
using Shoko.QueueProcessor.Workers;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// What a job type can do while it runs, derived from how it is wired rather than declared by it.
/// </summary>
internal static class JobCapabilities
{
    private static readonly ConcurrentDictionary<Type, bool> _cancellable = new();

    /// <summary>
    /// Whether a running job of <paramref name="jobType"/> can be cancelled: it takes an
    /// <see cref="IJobCancellationAccessor"/> in a public constructor, which is the only way it
    /// can see its cancellation token.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns><c>true</c> when the job observes cancellation.</returns>
    public static bool IsCancellable(Type jobType)
        => _cancellable.GetOrAdd(jobType, static type => type.GetConstructors()
            .Any(constructor => constructor.GetParameters()
                .Any(parameter => typeof(IJobCancellationAccessor).IsAssignableFrom(parameter.ParameterType))));
}
