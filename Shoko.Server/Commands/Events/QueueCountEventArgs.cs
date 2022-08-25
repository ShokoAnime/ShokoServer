using System;

namespace Shoko.Server.Commands
{
    public class QueueCountEventArgs : EventArgs
    {
        public readonly int QueueCount;

        public QueueCountEventArgs(int queueCount)
        {
            QueueCount = queueCount;
        }
    }
}