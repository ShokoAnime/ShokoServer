using System;
using System.Threading;
using System.Diagnostics;

namespace Shoko.Server.Repositories.ReaderWriterLockExtensions
{
    //https://itneverworksfirsttime.wordpress.com/2011/06/29/an-idisposable-locking-implementation/
    [DebuggerStepThrough]
    public static class ReaderWriterLockSlimExtension
    {
        public static IDisposable ReaderLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.Write);
        }

        public static IDisposable UpgradeableReaderLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.Write);
        }

        public static IDisposable WriterLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.Write);
        }
    }
}