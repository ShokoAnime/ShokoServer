using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Server;

using QueueStateEnum = Shoko.Models.Queue.QueueStateEnum;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Queue
{
    /// <summary>
    /// The name of the queue.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The current status of the queue.
    /// </summary>
    public QueueStatus Status { get; }

    /// <summary>
    /// The current queue item id. Makes it easier knowing when we switched to
    /// processing a new queue item.
    /// </summary>
    public int? CurrentItemID { get; set; }

    /// <summary>
    /// Describes what is happening to the current queue item in the queue,
    /// otherwise null.
    /// </summary>
    public string? CurrentMessage { get; }

    /// <summary>
    /// The number of items currently in the queue.
    /// </summary>
    public int Size { get; }

    public Queue(CommandProcessor processor)
    {
        // only create a deep-copy of the queue state once, then re-use it.
        var queueState = processor.QueueState;

        Name = processor.QueueType;
        CurrentItemID = processor.CurrentCommand?.CommandRequestID;
        Status = processor.Paused ? (
            // Check if it's still running even though it should be stopped.
            CurrentItemID != null ? QueueStatus.Pausing : QueueStatus.Paused
        ) : queueState.queueState == QueueStateEnum.Idle ? (
            // Check if it's actually idle, or if it's waiting to resume work.
            processor.QueueCount > 0 ? QueueStatus.Waiting : QueueStatus.Idle
        ) : (
            // It's currently running a command.
            QueueStatus.Running
        );
        // Show the current message if it's running or stopping.
        CurrentMessage = Status == QueueStatus.Running || Status == QueueStatus.Pausing ? queueState.formatMessage() ?? null : null;
        Size = processor.QueueCount;
    }

    public class QueueItem
    {
        /// <summary>
        /// The queue item id. This is unique to the queue item, as opposed to
        /// <see cref="Name"/>, which may have been used previously and might be
        /// used in the future by different queue items.
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// The name of the queue item. This can be shared across multiple
        /// queue items over the life span of the queue, but only one item will
        /// exist with the same name at any given time.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The queue item type.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public CommandRequestType Type { get; }

        /// <summary>
        /// Indicates the item is currently actively running in the queue.
        /// </summary>
        public bool IsRunning { get; }

        /// <summary>
        /// Indicates the item is currently disabled because it cannot run under
        /// the current conditions (e.g. an UDP or HTTP ban is active, etc.).
        /// </summary>
        public bool IsDisabled { get; }

        public QueueItem(CommandProcessor processor, CommandRequest request, bool httpBanned, bool udpBanned, bool udpUnavailable)
        {
            ID = request.CommandRequestID;
            Name = request.CommandID;
            Type = (CommandRequestType)request.CommandType;
            IsRunning = processor.CurrentCommand != null && processor.CurrentCommand.CommandRequestID == request.CommandRequestID;
            IsDisabled = Repositories.RepoFactory.CommandRequest.CheckIfCommandRequestIsDisabled(Type, httpBanned, udpBanned, udpUnavailable);
        }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum QueueStatus
    {
        Paused = 0,
        Idle = 1,
        Waiting = 2,
        Running = 3,
        Pausing = 4,
    }
}
