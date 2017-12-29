using System;
using Shoko.Commons.Queue;

namespace Shoko.Server.Commands
{
    public class QueueStateEventArgs : EventArgs
    {
        public readonly QueueStateStruct QueueState;

        public QueueStateEventArgs(QueueStateStruct queueState)
        {
            QueueState = queueState;
        }
    }
}