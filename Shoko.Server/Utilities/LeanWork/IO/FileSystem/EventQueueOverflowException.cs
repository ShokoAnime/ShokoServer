using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeanWork.IO.FileSystem.Watcher.LeanWork.IO.FileSystem
{
    [Serializable]
    class EventQueueOverflowException : Exception
    {
        public EventQueueOverflowException()
            : base()
        {
        }

        public EventQueueOverflowException(string message)
            : base(message)
        {
        }
    }
}