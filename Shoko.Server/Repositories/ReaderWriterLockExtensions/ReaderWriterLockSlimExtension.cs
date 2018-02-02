using System;
using System.Threading;

namespace Shoko.Server.Repositories.ReaderWriterLockExtensions
{
    //https://itneverworksfirsttime.wordpress.com/2011/06/29/an-idisposable-locking-implementation/
    public static class ReaderWriterLockSlimExtension
    {
        public static IDisposable ReaderLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.Read);
        }

        public static IDisposable UpgradeableReaderLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.UpgradeableRead);
        }

        public static IDisposable WriterLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.Write);
        }
    }
}