using System;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Lets the host carry "who asked for it" across the queue. The scheduler captures the actor
/// when a job is queued and stores it with the job; the worker restores it around the job's
/// <see cref="IQueueJob.Process"/>. Optional: without one registered, jobs carry no actor.
/// </summary>
/// <remarks>
/// Jobs queued from a job, directly, through <c>RunAfterCurrent</c> or as a chain, capture the
/// actor restored for that job, so they inherit it.
/// </remarks>
public interface IJobActorAccessor
{
    /// <summary>
    /// Captures the actor of the current flow, for a job about to be queued.
    /// </summary>
    /// <returns>The actor to store with the job, or <c>null</c> for none.</returns>
    JobActor? Capture();

    /// <summary>
    /// Makes the stored actor the actor of the current flow while the job runs.
    /// </summary>
    /// <param name="actor">
    /// The actor stored with the job, or <c>null</c> when it was queued without one,
    /// in which case the job runs for no one, whatever the worker's flow held.
    /// </param>
    /// <returns>A scope that puts back the previous actor once disposed.</returns>
    IDisposable Restore(JobActor? actor);

    /// <summary>
    /// Whether <see cref="Restore"/> can look a stored actor up right now. While it cannot, the
    /// queue holds every job stored with an actor, without using a retry, rather than run it for
    /// no one. Jobs without an actor are not held.
    /// </summary>
    bool CanRestore { get => true; }

    /// <summary>
    /// Raised when <see cref="CanRestore"/> changes, so the queue can let held jobs go.
    /// </summary>
    event EventHandler? CanRestoreChanged { add { } remove { } }
}

/// <summary>
/// Who a queued job was queued for, as stored with it. Holds no secret: the host looks the
/// credentials up again when the job runs.
/// </summary>
/// <param name="UserID">The ID of the user.</param>
/// <param name="DeviceName">The device name the user's credentials were issued to.</param>
public readonly record struct JobActor(int UserID, string DeviceName);
