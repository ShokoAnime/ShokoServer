namespace Shoko.Server
{
   
    public enum CommandLimiterType
    {
        None = -1,
        AniDB = 0,
        TvDB = 1
    }

    public enum HashSource
    {
        DirectHash = 1, // the file was hashed by the user
        WebCacheFileName = 2 // the hash was retrieved from the web cache based on file name
    }

    public enum RenamingType
    {
        Raw = 1,
        MetaData = 2
    }


    public enum ScheduledUpdateType
    {
        AniDBCalendar = 1,
        TvDBInfo = 2,
        AniDBUpdates = 3,
        AniDBTitles = 4,
        AniDBMyListSync = 5,
        TraktSync = 6,
        TraktUpdate = 7,
        MALUpdate = 8,
        AniDBMylistStats = 9,
        AniDBFileUpdates = 10,
        LogClean = 11,
        AzureUserInfo = 12,
        TraktToken = 13,
        DayFiltersUpdate = 14
    }


    public enum TvDBImageNodeType
    {
        Series = 1,
        Season = 2
    }


    public enum TraktSyncAction
    {
        Add = 1,
        Remove = 2
    }

    public enum AniDBPause
    {
        Long = 1,
        Short = 2
    }


    public enum FileHashType
    {
        ED2K = 0,
        MD5 = 1,
        SHA1 = 2,
        CRC32 = 3
    }
}
