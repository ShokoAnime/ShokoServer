using System;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Supplies a concurrency limit for job types that cannot carry one as an attribute, such as the
/// closed types of a generic job whose limit depends on its type argument.
/// </summary>
/// <remarks>
/// Registered in DI; every registration is asked in turn and the first answer wins. A job type
/// with a <see cref="Concurrency.LimitConcurrencyAttribute"/> or
/// <see cref="Concurrency.DisallowConcurrentExecutionAttribute"/> keeps the attribute's limit.
/// The limit is read when the pools are built and again whenever the orchestrator checks whether
/// a job may start, so it may become known after the container is built.
/// </remarks>
public interface IJobConcurrencyProvider
{
    /// <summary>
    /// The most jobs of a type that may run at once.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>The limit, or <see langword="null"/> when this provider has none for the type.</returns>
    int? GetConcurrencyLimit(Type jobType);
}
