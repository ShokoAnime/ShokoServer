namespace Shoko.Server.Repositories.ReaderWriterLockExtensions
{
    //https://itneverworksfirsttime.wordpress.com/2011/06/29/an-idisposable-locking-implementation/
    public enum LockType
    {
        Read,
        UpgradeableRead,
        Write,
    }
}