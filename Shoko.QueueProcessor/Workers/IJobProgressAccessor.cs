using System;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Scoped service that lets the currently-executing job report how far along it is. Inject it
/// into a job and call <see cref="IProgress{T}.Report"/> on <see cref="Progress"/> with a
/// percentage from 0 to 100.
/// </summary>
/// <remarks>
/// Progress is held in memory only and shown on the running job in the queue snapshot. A job
/// shows no progress until it first reports, so report 0 as soon as the job knows it will
/// report, and a job that never reports shows none. Values are clamped to 0 to 100. Outside a
/// worker (for example under <see cref="Abstractions.IJobFactory.Execute{T}"/>) the reports go
/// nowhere.
/// </remarks>
public interface IJobProgressAccessor
{
    /// <summary>
    /// The progress reporter for the currently-executing job, taking a percentage from 0 to 100.
    /// Never <see langword="null"/>.
    /// </summary>
    IProgress<decimal> Progress { get; }
}
