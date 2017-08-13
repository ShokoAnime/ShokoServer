using System;
using Shoko.Commons.Properties;
using Shoko.Models.Queue;

// ReSharper disable InconsistentNaming

namespace Shoko.Commons.Queue
{

    public struct QueueStateStruct
    {
        public QueueStateEnum queueState;
        public string[] extraParams;
        public string formatMessage()
        {
            string formatString = getFormatString(queueState);
            // ReSharper disable once CoVariantArrayConversion
            return string.Format(formatString, extraParams);
        }
        private string getFormatString(QueueStateEnum id)
        {
            switch (id)
            {
                case QueueStateEnum.AnimeInfo:
                    return Resources.Command_AnimeInfo;
                case QueueStateEnum.DeleteError:
                    return Resources.Command_DeleteError;
                case QueueStateEnum.DownloadImage:
                    return Resources.Command_DownloadImage;
                case QueueStateEnum.DownloadMalWatched:
                    return Resources.Command_DownloadMalWatched;
                case QueueStateEnum.DownloadTvDBImages:
                    return Resources.Command_DownloadTvDBImages;
                case QueueStateEnum.FileInfo:
                    return Resources.Command_FileInfo;
                case QueueStateEnum.GetCalendar:
                    return Resources.Command_GetCalendar;
                case QueueStateEnum.GetEpisodeList:
                    return Resources.Command_GetEpisodeList;
                case QueueStateEnum.GetFileInfo:
                    return Resources.Command_GetFileInfo;
                case QueueStateEnum.GetReleaseGroup:
                    return Resources.Command_GetReleaseGroup;
                case QueueStateEnum.GetReleaseInfo:
                    return Resources.Command_GetReleaseInfo;
                case QueueStateEnum.GetReviewInfo:
                    return Resources.Command_GetReviewInfo;
                case QueueStateEnum.GettingTvDB:
                    return Resources.Command_GettingTvDB;
                case QueueStateEnum.GetUpdatedAnime:
                    return Resources.Command_GetUpdatedAnime;
                case QueueStateEnum.HashingFile:
                    return Resources.Command_HashingFile;
                case QueueStateEnum.CheckingFile:
                    return Resources.Command_CheckingFile;
                case QueueStateEnum.Idle:
                    return Resources.Command_Idle;
                case QueueStateEnum.Paused:
                    return Resources.Command_Paused;
                case QueueStateEnum.Queued:
                    return Resources.Command_Queued;
                case QueueStateEnum.ReadingMedia:
                    return Resources.Command_ReadingMedia;
                case QueueStateEnum.Refresh:
                    return Resources.Command_Refresh;
                case QueueStateEnum.SearchMal:
                    return Resources.Command_SearchMal;
                case QueueStateEnum.SearchTMDb:
                    return Resources.Command_SearchTMDb;
                case QueueStateEnum.SearchTrakt:
                    return Resources.Command_SearchTrakt;
                case QueueStateEnum.SearchTvDB:
                    return Resources.Command_SearchTvDB;
                case QueueStateEnum.SendAnimeAzure:
                    return Resources.Command_SendAnimeAzure;
                case QueueStateEnum.SendAnimeFull:
                    return Resources.Command_SendAnimeFull;
                case QueueStateEnum.SendAnimeTitle:
                    return Resources.Command_SendAnimeTitle;
                case QueueStateEnum.SendAnonymousData:
                    return Resources.Command_SendAnonymousData;
                case QueueStateEnum.StartingGeneral:
                    return Resources.Command_StartingGeneral;
                case QueueStateEnum.StartingHasher:
                    return Resources.Command_StartingHasher;
                case QueueStateEnum.StartingImages:
                    return Resources.Command_StartingImages;
                case QueueStateEnum.SyncMyList:
                    return Resources.Command_SyncMyList;
                case QueueStateEnum.SyncTrakt:
                    return Resources.Command_SyncTrakt;
                case QueueStateEnum.SyncTraktEpisodes:
                    return Resources.Command_SyncTraktEpisodes;
                case QueueStateEnum.SyncTraktSeries:
                    return Resources.Command_SyncTraktSeries;
                case QueueStateEnum.SyncVotes:
                    return Resources.Command_SyncVotes;
                case QueueStateEnum.TraktAddHistory:
                    return Resources.Command_TraktAddHistory;
                case QueueStateEnum.UpdateMALWatched:
                    return Resources.Command_UpdateMALWatched;
                case QueueStateEnum.UpdateMyListInfo:
                    return Resources.Command_UpdateMyListInfo;
                case QueueStateEnum.UpdateMyListStats:
                    return Resources.Command_UpdateMyListStats;
                case QueueStateEnum.UpdateTrakt:
                    return Resources.Command_UpdateTrakt;
                case QueueStateEnum.UpdateTraktData:
                    return Resources.Command_UpdateTraktData;
                case QueueStateEnum.UploadMALWatched:
                    return Resources.Command_UploadMALWatched;
                case QueueStateEnum.VoteAnime:
                    return Resources.Command_VoteAnime;
                case QueueStateEnum.WebCacheDeleteXRefAniDBMAL:
                    return Resources.Command_WebCacheDeleteXRefAniDBMAL;
                case QueueStateEnum.WebCacheDeleteXRefAniDBOther:
                    return Resources.Command_WebCacheDeleteXRefAniDBOther;
                case QueueStateEnum.WebCacheDeleteXRefAniDBTrakt:
                    return Resources.Command_WebCacheDeleteXRefAniDBTrakt;
                case QueueStateEnum.WebCacheDeleteXRefAniDBTvDB:
                    return Resources.Command_WebCacheDeleteXRefAniDBTvDB;
                case QueueStateEnum.WebCacheDeleteXRefFileEpisode:
                    return Resources.Command_WebCacheDeleteXRefFileEpisode;
                case QueueStateEnum.WebCacheSendXRefAniDBMAL:
                    return Resources.Command_WebCacheSendXRefAniDBMAL;
                case QueueStateEnum.WebCacheSendXRefAniDBOther:
                    return Resources.Command_WebCacheSendXRefAniDBOther;
                case QueueStateEnum.WebCacheSendXRefAniDBTrakt:
                    return Resources.Command_WebCacheSendXRefAniDBTrakt;
                case QueueStateEnum.WebCacheSendXRefAniDBTvDB:
                    return Resources.Command_WebCacheSendXRefAniDBTvDB;
                case QueueStateEnum.WebCacheSendXRefFileEpisode:
                    return Resources.Command_WebCacheSendXRefFileEpisode;
                case QueueStateEnum.AniDB_MyListAdd:
                    return Resources.AniDB_MyListAdd;
                case QueueStateEnum.AniDB_MyListDelete:
                    return Resources.AniDB_MyListDelete;
                case QueueStateEnum.AniDB_GetTitles:
                    return Resources.AniDB_GetTitles;
                case QueueStateEnum.Actions_SyncVotes:
                    return Resources.Actions_SyncVotes;
                case QueueStateEnum.LinkAniDBTvDB:
                    return Resources.Command_LinkAniDBTvDB;
                case QueueStateEnum.RefreshGroupFilter:
                    return Resources.Command_RefreshGroupFilter;
                case QueueStateEnum.SyncPlex:
                    return Resources.Command_SyncPlex;
                case QueueStateEnum.LinkFileManually:
                    return Resources.Command_LinkFileManually;
                case QueueStateEnum.AniDB_MyListGetFile:
                    return Resources.AniDB_MyListGetFile;
                default:
                    throw new Exception("Unknown queue state format string");
            }

        }
    }

}
