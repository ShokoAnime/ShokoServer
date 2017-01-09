using System;

namespace Shoko.Server
{


    public enum CommandRequestType
    {
        ProcessFile = 1,
        AniDB_GetAnimeHTTP = 2,
        AniDB_GetAnimeUDP = 3,
        AniDB_GetFileUDP = 4,
        AniDB_AddFileUDP = 5,
        AniDB_UpdateWatchedUDP = 6,
        TvDBSearch = 7,
        AniDB_GetCharsCreators = 8,
        AniDB_GetCharacter = 9,
        AniDB_GetCreator = 10,
        HashFile = 11,
        //WebCache_SendFileHash = 12,
        WebCache_SendXRefFileEpisode = 14,
        WebCache_DeleteXRefFileEpisode = 15,
        WebCache_DeleteXRefTvDB = 16,
        WebCache_DeleteXRefAniDBTvDB = 17,
        WebCache_SendXRefAniDBTvDB = 18,
        AniDB_GetReviews = 20,
        AniDB_GetReleaseGroupStatus = 21,
        AniDB_GetUpdated = 22,
        AniDB_SyncMyList = 23,
        AniDB_GetReleaseGroup = 24,
        AniDB_GetCalendar = 25,
        AniDB_GetTitles = 26,
        AniDB_SyncVotes = 27,
        AniDB_VoteAnime = 28,
        AniDB_VoteEpisode = 29,
        TvDB_SeriesEpisodes = 30,
        TvDB_DownloadImages = 31,
        TvDB_SearchAnime = 32,
        ImageDownload = 33,
        AniDB_DeleteFileUDP = 34,
        WebCache_SendXRefAniDBOther = 35,
        WebCache_DeleteXRefAniDBOther = 36,
        MovieDB_SearchAnime = 37,
        Trakt_SearchAnime = 38,
        WebCache_SendXRefAniDBTrakt = 39,
        WebCache_DeleteXRefAniDBTrakt = 40,
        Trakt_UpdateInfoImages = 41,
        Trakt_EpisodeHistory = 42,
        Trakt_SyncCollection = 43,
        Trakt_SyncCollectionSeries = 44,
        Trakt_EpisodeCollection = 45,
        Trakt_UpdateAllSeries = 46,
        ReadMediaInfo = 50,
        WebCache_SendXRefAniDBMAL = 51,
        WebCache_DeleteXRefAniDBMAL = 52,
        MAL_SearchAnime = 60,
        MAL_UpdateStatus = 61,
        MAL_UploadWatchedStates = 62,
        MAL_DownloadWatchedStates = 63,
        //WebCache_SendAniDB_File = 64,
        //WebCache_GetAniDB_File = 65,
        AniDB_UpdateMylistStats = 66,
        Azure_SendAnimeFull = 70,
        Azure_SendAnimeXML = 71,
        Azure_SendAnimeTitle = 72,
        Azure_SendUserInfo = 73,
        AniDB_GetEpisodeUDP = 80,
        Refresh_AnimeStats = 90,
        LinkAniDBTvDB = 91,
	    Refresh_GroupFilter = 92
    }

    public enum CommandRequestPriority
    {
        Priority1 = 1,
        Priority2 = 2,
        Priority3 = 3,
        Priority4 = 4,
        Priority5 = 5,
        Priority6 = 6,
        Priority7 = 7,
        Priority8 = 8,
        Priority9 = 9,
        Priority10 = 10,
        Priority11 = 11,
        Default = 99
    }

    public enum HashSource
    {
        DirectHash = 1, // the file was hashed by the user
        WebCacheFileName = 2 // the hash was retrieved from the web cache based on file name
    }

    public enum CrossRefSource
    {
        AniDB = 1,
        User = 2,
        WebCache = 3
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




    public enum ImageDownloadEventType
    {
        Started = 1,
        Complete = 2
    }


    public enum TvDBImageNodeType
    {
        Series = 1,
        Season = 2
    }

    public enum CrossRefType
    {
        MovieDB = 1,
        MyAnimeList = 2,
        AnimePlanet = 3,
        BakaBT = 4,
        TraktTV = 5,
        AnimeNano = 6,
        CrunchRoll = 7,
        Konachan = 8
    }

    public enum ScheduledUpdateFrequency
    {
        Never = 1,
        HoursSix = 2,
        HoursTwelve = 3,
        Daily = 4,
        WeekOne = 5,
        MonthOne = 6
    }


    public enum StatCountType
    {
        Watched = 1,
        Played = 2,
        Stopped = 3
    }

    public enum DataSourceType
    {
        AniDB = 1,
        TheTvDB = 2
    }

    public enum TraktActivityAction
    {
        Scrobble = 1,
        Comment = 2
    }

    public enum TraktActivityType
    {
        Episode = 1,
        Show = 2
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

    public enum FileSearchCriteria
    {
        Name = 1,
        Size = 2,
        LastOneHundred = 3,
        ED2KHash = 4
    }

    public enum FileHashType
    {
        ED2K = 0,
        MD5 = 1,
        SHA1 = 2,
        CRC32 = 3
    }
}