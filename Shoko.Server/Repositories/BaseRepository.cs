namespace Shoko.Server.Repositories
{
    public class BaseRepository
    {
        internal static readonly object GlobalDBLock = new();
    }
}
