using Shoko.Models.Queue;

namespace Shoko.Server.API.SignalR.Models;

public class QueueStateSignalRModel
{
    /// <summary>
    /// The current state of the queue, as an enum value.
    /// </summary>
    public QueueStateEnum State { get; set; }

    /// <summary>
    /// This is the verbose version of the current state of the queue, and the
    /// description of what is happening to the current command in the queue.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The current command id. Makes it easier knowing when we switched to
    /// processing a new command.
    /// </summary>
    public int? CurrentCommandID { get; set; }
}
