using System;
using Shoko.Commons.Queue;

namespace Shoko.Server.Commands;

public class QueueStateEventArgs : EventArgs
{
    public readonly QueueStateStruct QueueState;

    public readonly int? CommandRequestID;

    public readonly int QueueCount;

    public readonly bool IsPaused;

    public QueueStateEventArgs(QueueStateStruct queueState, int? commandRequestID, int currentCount, bool isPaused)
    {
        QueueState = queueState;
        CommandRequestID = commandRequestID;
        QueueCount = currentCount;
        IsPaused = isPaused;
    }
}
