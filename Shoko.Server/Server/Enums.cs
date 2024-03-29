namespace Shoko.Server.Server;

public enum HashSource
{
    DirectHash = 1, // the file was hashed by the user
    FileNameCache = 2 // the hash was retrieved from the web cache based on file name
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

public enum TraktSyncAction
{
    Add = 1,
    Remove = 2
}
