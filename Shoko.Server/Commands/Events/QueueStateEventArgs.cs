using System;
using Shoko.Commons.Queue;

namespace Shoko.Server.Commands;

public class QueueStateEventArgs : EventArgs
{
    public readonly QueueStateStruct QueueState;

    public readonly int? CommandRequestID;

    public QueueStateEventArgs(QueueStateStruct queueState, int? commandRequestID)
    {
        QueueState = queueState;
        CommandRequestID = commandRequestID;
    }
}
