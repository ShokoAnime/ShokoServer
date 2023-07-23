using System;
using Shoko.Commons.Queue;

namespace Shoko.Server.Commands;

public class QueueStateEventArgs : EventArgs
{
    public readonly QueueStateStruct QueueState;

    public readonly int? CommandRequestID;

    public readonly int CurrentCount;

    public readonly bool IsPaused;

    public readonly bool IsRunning;

    public QueueStateEventArgs(QueueStateStruct queueState, int? commandRequestID, int currentCount, bool isPaused, bool isRunning)
    {
        QueueState = queueState;
        CommandRequestID = commandRequestID;
        CurrentCount = currentCount;
        IsPaused = isPaused;
        IsRunning = isRunning;
    }
}
