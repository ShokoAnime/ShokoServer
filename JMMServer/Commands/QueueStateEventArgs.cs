using System;

namespace JMMServer.Commands
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