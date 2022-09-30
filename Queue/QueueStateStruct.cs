using System;
using System.Diagnostics.Contracts;
using Shoko.Commons.Properties;
using Shoko.Models.Queue;

// ReSharper disable InconsistentNaming

namespace Shoko.Commons.Queue
{

    public struct QueueStateStruct
    {
        public QueueStateEnum queueState;
        public string[] extraParams;
        public string message { get; set; }

        [Pure]
        public string formatMessage()
        {
            if (!string.IsNullOrEmpty(message))
                return string.Format(message, extraParams);

            var formatString = getFormatString(queueState);
            // ReSharper disable once CoVariantArrayConversion
            return string.Format(formatString, extraParams);
        }

        [Pure]
        private string getFormatString(QueueStateEnum id)
        {
            switch (id)
            {
                case QueueStateEnum.Actions_SyncVotes:   return "Upload Local Votes To AniDB.";
                case QueueStateEnum.AniDB_GetTitles:     return "Getting AniDB titles";
                case QueueStateEnum.AniDB_MyListAdd:     return "Adding file to MyList: {0}";
                case QueueStateEnum.AniDB_MyListDelete:  return "Deleting file from MyList: {0}";
                case QueueStateEnum.AniDB_MyListGetFile: return "Getting File Status from MyList: {0} ({1})";
                case QueueStateEnum.AnimeInfo:           return "Getting anime info from HTTP API: {0}";
                case QueueStateEnum.CheckingFile:        return "Checking File for Hashes: {0}";
                case QueueStateEnum.DeleteError:         return "Error deleting image: ({0}) - {1}";
                case QueueStateEnum.DownloadImage:       return "Downloading Image {0}: {1}";
                case QueueStateEnum.DownloadMalWatched:  return "Downloading watched states from MAL";
                case QueueStateEnum.DownloadTvDBImages:  return "Getting images from The TvDB: {0}";
                case QueueStateEnum.FileInfo:            return "Getting file info from UDP API: {0}";
                case QueueStateEnum.GetCalendar:         return "Getting calendar info from UDP API";
                case QueueStateEnum.GetEpisodeList:      return "Getting episode info from UDP API: {0}";
                case QueueStateEnum.GetFileInfo:         return "Getting file info from UDP API: {0}";
                case QueueStateEnum.GetReleaseGroup:     return "Getting group status info from UDP API for Anime: {0}";
                case QueueStateEnum.GetReleaseInfo:      return "Getting release group info from UDP API: {0}";
                case QueueStateEnum.GetReviewInfo:       return "Getting review info from UDP API for Anime: {0}";
                case QueueStateEnum.GetUpdatedAnime:     return "Getting list of updated anime from UDP API";
                case QueueStateEnum.GettingTvDBEpisode:  return "Updating TvDB Episode: {0}";
                case QueueStateEnum.GettingTvDBSeries:   return "Updating TvDB Series: {0}";
                case QueueStateEnum.HashingFile:         return "Hashing File: {0}";
                case QueueStateEnum.Idle:                return "Idle";
                case QueueStateEnum.LinkAniDBTvDB:       return "Updating Changed TvDB association: {0}";
                case QueueStateEnum.LinkFileManually:    return "Linking File: {0} to Episode: {1}";
                case QueueStateEnum.Paused:              return "Paused";
                case QueueStateEnum.Queued:              return "Queued";
                case QueueStateEnum.ReadingMedia:        return "Reading media info for file: {0}";
                case QueueStateEnum.Refresh:             return "Refreshing anime stats: {0}";
                case QueueStateEnum.RefreshGroupFilter:  return "Refreshing Group Filter: {0}";
                case QueueStateEnum.SearchMal:           return "Searching for anime on MAL: {0}";
                case QueueStateEnum.SearchTMDb:          return "Searching for anime on The MovieDB: {0}";
                case QueueStateEnum.SearchTrakt:         return "Searching for anime on Trakt.TV: {0}";
                case QueueStateEnum.SearchTvDB:          return "Searching for anime on The TvDB: {0}";
                case QueueStateEnum.StartingGeneral:     return "Starting general command worker";
                case QueueStateEnum.StartingHasher:      return "Starting hasher command worker";
                case QueueStateEnum.StartingImages:      return "Starting image downloading command worker";
                case QueueStateEnum.SyncMyList:          return "Syncing MyList info from HTTP API";
                case QueueStateEnum.SyncPlex:            return "Syncing Plex for user: {0}";
                case QueueStateEnum.SyncTrakt:           return "Syncing Trakt collection";
                case QueueStateEnum.SyncTraktEpisodes:   return "Sync episode to collection on Trakt: {0} - {1}";
                case QueueStateEnum.SyncTraktSeries:     return "Syncing Trakt collection for series: {0}";
                case QueueStateEnum.SyncVotes:           return "Syncing vote info from HTTP API";
                case QueueStateEnum.TraktAddHistory:     return "Add episode to history on Trakt: {0}";
                case QueueStateEnum.UpdateMALWatched:    return "Updating watched status on MAL: {0}";
                case QueueStateEnum.UpdateMyListInfo:    return "Updating MyList info from UDP API for File: {0}";
                case QueueStateEnum.UpdateMyListStats:   return "Updating AniDB MyList Stats";
                case QueueStateEnum.UpdateTrakt:         return "Updating all Trakt series info added to queue";
                case QueueStateEnum.UpdateTraktData:     return "Updating info/images on Trakt.TV: {0}";
                case QueueStateEnum.UploadMALWatched:    return "Uploading watched states to MAL";
                case QueueStateEnum.ValidateAllImages:   return "Validating Images {0}";
                case QueueStateEnum.VoteAnime:           return "Voting: {0} - {1}";
                default:                                 throw new Exception("Unknown queue state format string");
            }
        }
    }

}
