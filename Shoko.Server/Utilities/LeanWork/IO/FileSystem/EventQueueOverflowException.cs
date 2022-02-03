using System;

namespace LeanWork.IO.FileSystem.Watcher.LeanWork.IO.FileSystem
{
    [Serializable]
    class EventQueueOverflowException : Exception
    {
        public EventQueueOverflowException()
        {
        }

        public EventQueueOverflowException(string message)
            : base(message)
        {
        }
    }
}