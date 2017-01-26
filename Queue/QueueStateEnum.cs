// ReSharper disable InconsistentNaming

namespace Shoko.Models.Queue
{
    public enum QueueStateEnum
    {
        AnimeInfo = 1, DeleteError, DownloadImage, DownloadMalWatched, DownloadTvDBImages, FileInfo, GetCalendar, GetEpisodeList, GetFileInfo, GetReleaseGroup,
        GetReleaseInfo, GetReviewInfo, GettingTvDB, GetUpdatedAnime, HashingFile, Idle, Paused, Queued, ReadingMedia, Refresh, SearchMal, SearchTMDb, SearchTrakt,
        SearchTvDB, SendAnimeAzure, SendAnimeFull, SendAnimeTitle, SendAnonymousData, StartingGeneral, StartingHasher, StartingImages, SyncMyList, SyncTrakt,
        SyncTraktEpisodes, SyncTraktSeries, SyncVotes, TraktAddHistory, UpdateMALWatched, UpdateMyListInfo, UpdateMyListStats, UpdateTrakt, UpdateTraktData, UploadMALWatched,
        VoteAnime, WebCacheDeleteXRefAniDBMAL, WebCacheDeleteXRefAniDBOther, WebCacheDeleteXRefAniDBTrakt, WebCacheDeleteXRefAniDBTvDB, WebCacheDeleteXRefFileEpisode, WebCacheSendXRefAniDBMAL,
        WebCacheSendXRefAniDBOther, WebCacheSendXRefAniDBTrakt, WebCacheSendXRefAniDBTvDB, WebCacheSendXRefFileEpisode, AniDB_MyListAdd, AniDB_MyListDelete, AniDB_GetTitles, Actions_SyncVotes,
        LinkAniDBTvDB
    }
}
