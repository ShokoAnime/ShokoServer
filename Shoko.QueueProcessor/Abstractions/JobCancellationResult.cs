namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// What a request to cancel or remove a queued job did, returned by
/// <see cref="IQueueScheduler.Cancel"/>, <see cref="QueueHandler.Cancel"/> and <see cref="QueueHandler.Remove"/>.
/// </summary>
public enum JobCancellationResult
{
    /// <summary>
    /// No waiting or running job has the key.
    /// </summary>
    NotFound = 0,

    /// <summary>
    /// The job was waiting and is gone from the queue. Its key is free, so it can be queued again.
    /// </summary>
    Removed = 1,

    /// <summary>
    /// The job is running and was asked to stop. It stays in the queue snapshot, marked as
    /// cancellation requested, until it stops; it then ends as cancelled, as completed when it
    /// finished before noticing, or as failed, and is never retried. Asking again changes nothing.
    /// </summary>
    CancellationRequested = 2,

    /// <summary>
    /// The job is running and does not observe cancellation, so it was left alone.
    /// </summary>
    NotCancellable = 3,

    /// <summary>
    /// The job is running and was left alone, because only a waiting job was to be removed.
    /// </summary>
    Running = 4,
}
