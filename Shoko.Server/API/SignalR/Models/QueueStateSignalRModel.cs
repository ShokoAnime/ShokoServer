using Newtonsoft.Json;
using Shoko.Models.Queue;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class QueueStateSignalRModel
{
    /// <summary>
    /// The current state of the queue, as an enum value.
    /// </summary>
    public QueueStateEnum State { get; }

    /// <summary>
    /// This is the verbose version of the current state of the queue, and the
    /// description of what is happening to the current command in the queue.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The current command id. Makes it easier knowing when we switched to
    /// processing a new command.
    /// </summary>
    public int? CurrentCommandID { get; }

    /// <summary>
    /// The current number of commands in the queue.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? QueueCount { get; }

    /// <summary>
    /// The queue status.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; }

    public QueueStateSignalRModel(QueueStateEventArgs eventArgs, bool legacy = false)
    {
        if (legacy)
        {
            State = eventArgs.IsPaused ? QueueStateEnum.Paused : eventArgs.QueueState.queueState;
            Description = eventArgs.IsPaused ? "Paused" : eventArgs.QueueState.formatMessage();
        }
        else {
            State = eventArgs.QueueState.queueState;
            Description = eventArgs.QueueState.formatMessage();
            CurrentCommandID = eventArgs.CommandRequestID;
            QueueCount = eventArgs.QueueCount;
            Status = eventArgs.IsPaused ? (
                // Check if it's still running even though it should be stopped.
                CurrentCommandID != null ? "Pausing" : "Paused"
            ) : eventArgs.QueueState.queueState == QueueStateEnum.Idle ? (
                // Check if it's actually idle, or if it's waiting to resume work.
                eventArgs.QueueCount > 0 ? "Waiting" : "Idle"
            ) : (
                // It's currently running a command.
                "Running"
            );
        }
    }

    public QueueStateSignalRModel(CommandProcessor processor, bool legacy = false)
    {
        // only create a deep-copy of the queue state once, then re-use it.
        var queueState = processor.QueueState;
        if (legacy)
        {
            State = processor.Paused ? QueueStateEnum.Paused : queueState.queueState;
            Description = processor.Paused ? "Paused" : queueState.formatMessage();
        }
        else {
            State = queueState.queueState;
            Description = queueState.formatMessage();
            CurrentCommandID = processor.CurrentCommand?.CommandRequestID;
            QueueCount = processor.QueueCount;
            Status = processor.Paused ? (
                // Check if it's still running even though it should be stopped.
                CurrentCommandID != null ? "Pausing" : "Paused"
            ) : queueState.queueState == QueueStateEnum.Idle ? (
                // Check if it's actually idle, or if it's waiting to resume work.
                processor.QueueCount > 0 ? "Waiting" : "Idle"
            ) : (
                // It's currently running a command.
                "Running"
            );
        }
    }
}
