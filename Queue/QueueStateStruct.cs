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
            return id switch
            {
                QueueStateEnum.Actions_SyncVotes => "Upload Local Votes To AniDB.",
                QueueStateEnum.AniDB_GetTitles => "Getting AniDB titles",
                QueueStateEnum.AniDB_MyListAdd => "Adding file to MyList: {0}",
                QueueStateEnum.AniDB_MyListDelete => "Deleting file from MyList: {0}",
                QueueStateEnum.AniDB_MyListGetFile => "Getting File Status from MyList: {0} ({1})",
                QueueStateEnum.AnimeInfo => "Getting anime info from HTTP API: {0}",
                QueueStateEnum.CheckingFile => "Checking File for Hashes: {0}",
                QueueStateEnum.DeleteError => "Error deleting image: ({0}) - {1}",
                QueueStateEnum.DownloadImage => "Downloading Image {0}: {1}",
                QueueStateEnum.DownloadMalWatched => "Downloading watched states from MAL",
                QueueStateEnum.DownloadTvDBImages => "Getting images from The TvDB: {0}",
                QueueStateEnum.FileInfo => "Getting file info from UDP API: {0}",
                QueueStateEnum.GetCalendar => "Getting calendar info from UDP API",
                QueueStateEnum.GetEpisodeList => "Getting episode info from UDP API: {0}",
                QueueStateEnum.GetFileInfo => "Getting file info from UDP API: {0}",
                QueueStateEnum.GetReleaseGroup => "Getting group status info from UDP API for Anime: {0}",
                QueueStateEnum.GetReleaseInfo => "Getting release group info from UDP API: {0}",
                QueueStateEnum.GetReviewInfo => "Getting review info from UDP API for Anime: {0}",
                QueueStateEnum.GetUpdatedAnime => "Getting list of updated anime from UDP API",
                QueueStateEnum.GettingTvDBEpisode => "Updating TvDB Episode: {0}",
                QueueStateEnum.GettingTvDBSeries => "Updating TvDB Series: {0}",
                QueueStateEnum.HashingFile => "Hashing File: {0}",
                QueueStateEnum.Idle => "Idle",
                QueueStateEnum.LinkAniDBTvDB => "Updating Changed TvDB association: {0}",
                QueueStateEnum.LinkFileManually => "Linking File: {0} to Episode: {1}",
                QueueStateEnum.Paused => "Paused",
                QueueStateEnum.Queued => "Queued",
                QueueStateEnum.ReadingMedia => "Reading media info for file: {0}",
                QueueStateEnum.Refresh => "Refreshing anime stats: {0}",
                QueueStateEnum.RefreshGroupFilter => "Refreshing Group Filter: {0}",
                QueueStateEnum.SearchMal => "Searching for anime on MAL: {0}",
                QueueStateEnum.SearchTMDb => "Searching for anime on The MovieDB: {0}",
                QueueStateEnum.SearchTrakt => "Searching for anime on Trakt.TV: {0}",
                QueueStateEnum.SearchTvDB => "Searching for anime on The TvDB: {0}",
                QueueStateEnum.StartingGeneral => "Starting general command worker",
                QueueStateEnum.StartingHasher => "Starting hasher command worker",
                QueueStateEnum.StartingImages => "Starting image downloading command worker",
                QueueStateEnum.SyncMyList => "Syncing MyList info from HTTP API",
                QueueStateEnum.SyncPlex => "Syncing Plex for user: {0}",
                QueueStateEnum.SyncTrakt => "Syncing Trakt collection",
                QueueStateEnum.SyncTraktEpisodes => "Sync episode to collection on Trakt: {0} - {1}",
                QueueStateEnum.SyncTraktSeries => "Syncing Trakt collection for series: {0}",
                QueueStateEnum.SyncVotes => "Syncing vote info from HTTP API",
                QueueStateEnum.TraktAddHistory => "Add episode to history on Trakt: {0}",
                QueueStateEnum.UpdateMALWatched => "Updating watched status on MAL: {0}",
                QueueStateEnum.UpdateMyListInfo => "Updating MyList info from UDP API for File: {0}",
                QueueStateEnum.UpdateMyListStats => "Updating AniDB MyList Stats",
                QueueStateEnum.UpdateTrakt => "Updating all Trakt series info added to queue",
                QueueStateEnum.UpdateTraktData => "Updating info/images on Trakt.TV: {0}",
                QueueStateEnum.UploadMALWatched => "Uploading watched states to MAL",
                QueueStateEnum.ValidateAllImages => "Validating Images {0}",
                QueueStateEnum.VoteAnime => "Voting: {0} - {1}",
                _ => throw new Exception("Unknown queue state format string")
            };
        }
    }

}
