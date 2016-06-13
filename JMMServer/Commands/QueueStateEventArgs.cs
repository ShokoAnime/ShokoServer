using System;

namespace JMMServer.Commands
{
    public class QueueStateEventArgs : EventArgs
    {
        public readonly string QueueState;

        public QueueStateEventArgs(string queueState)
        {
            QueueState = queueState;
        }
    }
}