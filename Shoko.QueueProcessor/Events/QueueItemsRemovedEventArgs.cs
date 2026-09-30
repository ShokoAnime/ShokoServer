using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Events;

/// <summary>
/// Fired when waiting jobs are removed from the queue, one by a user or all of them by a clear.
/// </summary>
public class QueueItemsRemovedEventArgs : EventArgs
{
    /// <summary>
    /// The keys of the removed jobs, or empty when the whole queue was cleared.
    /// </summary>
    public IReadOnlyList<string> RemovedKeys { get; init; } = [];
}
