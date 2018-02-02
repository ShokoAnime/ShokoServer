using System;
using System.Threading;

namespace Shoko.Server.Repositories.ReaderWriterLockExtensions
{
    //https://itneverworksfirsttime.wordpress.com/2011/06/29/an-idisposable-locking-implementation/
    public class DisposableLockWrapper : IDisposable
    {
        private readonly LockType lockType;
        private readonly ReaderWriterLockSlim readerWriterLock;

        public DisposableLockWrapper(ReaderWriterLockSlim readerWriterLock, LockType lockType)
        {
            this.readerWriterLock = readerWriterLock;
            this.lockType = lockType;

            switch (this.lockType)
            {
                case LockType.Read:
                    this.readerWriterLock.EnterReadLock();
                    break;

                case LockType.UpgradeableRead:
                    this.readerWriterLock.EnterUpgradeableReadLock();
                    break;

                case LockType.Write:
                    this.readerWriterLock.EnterWriteLock();
                    break;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed objects
                switch (lockType)
                {
                    case LockType.Read:
                        readerWriterLock.ExitReadLock();
                        break;

                    case LockType.UpgradeableRead:
                        readerWriterLock.ExitUpgradeableReadLock();
                        break;

                    case LockType.Write:
                        readerWriterLock.ExitWriteLock();
                        break;
                }
            }
        }
    }
}