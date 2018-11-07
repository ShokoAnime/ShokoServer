using System.Threading;

#pragma warning disable 4014

namespace Shoko.Server.Native.Hashing
{
    public class ThreadUnit
    {
        public bool Abort;
        public byte[][] Buffer;
        public int BufferNumber;
        public int CurrentSize;
        public string Error;
        public long FileSize;
        public AutoResetEvent MainAutoResetEvent = new AutoResetEvent(false);
        public AutoResetEvent WorkerAutoResetEvent = new AutoResetEvent(false);
        public Hasher WorkUnit;

        public void CancelWorker()
        {
            if (!Abort)
            {
                Abort = true;
                WorkerAutoResetEvent.Set();
                MainAutoResetEvent.WaitOne();
            }
        }

        public void WorkerError(string error)
        {
            if (Abort)
                return;
            Error = error;
            Abort = true;
        }
    }
}