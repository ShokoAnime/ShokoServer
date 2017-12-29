using FluentNHibernate.Mapping;
using Shoko.Server.Commands;

namespace Shoko.Server.Mappings
{
    public class CommandRequestMap : ClassMap<CommandRequest>
    {
        public CommandRequestMap()
        {
            Polymorphism.Implicit();
            Not.LazyLoad();
            Id(x => x.CommandRequestID);
            Map(x => x.CommandDetails).Not.Nullable();
            Map(x => x.CommandID).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.Priority).Not.Nullable();

            DiscriminateSubClassesOnColumn("CommandType").Not.Nullable();
        }
    }

    public class CommandRequest_AddFileToMyListMap : SubclassMap<CommandRequest_AddFileToMyList>
    {
        public CommandRequest_AddFileToMyListMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_AddFileUDP);
        }
    }

    public class CommandRequest_DeleteFileFromMyListMap : SubclassMap<CommandRequest_DeleteFileFromMyList>
    {
        public CommandRequest_DeleteFileFromMyListMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_DeleteFileUDP);
        }
    }

    public class CommandRequest_GetAnimeHTTPMap : SubclassMap<CommandRequest_GetAnimeHTTP>
    {
        public CommandRequest_GetAnimeHTTPMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetAnimeHTTP);
        }
    }

    public class CommandRequest_GetCalendarMap : SubclassMap<CommandRequest_GetCalendar>
    {
        public CommandRequest_GetCalendarMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetCalendar);
        }
    }

    public class CommandRequest_GetEpisodeMap : SubclassMap<CommandRequest_GetEpisode>
    {
        public CommandRequest_GetEpisodeMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetEpisodeUDP);
        }
    }

    public class CommandRequest_GetFileMap : SubclassMap<CommandRequest_GetFile>
    {
        public CommandRequest_GetFileMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetFileUDP);
        }
    }

    public class CommandRequest_GetFileMyListStatusMap : SubclassMap<CommandRequest_GetFileMyListStatus>
    {
        public CommandRequest_GetFileMyListStatusMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetMyListFile);
        }
    }

    public class CommandRequest_GetReleaseGroupMap : SubclassMap<CommandRequest_GetReleaseGroup>
    {
        public CommandRequest_GetReleaseGroupMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetReleaseGroup);
        }
    }

    public class CommandRequest_GetReleaseGroupStatusMap : SubclassMap<CommandRequest_GetReleaseGroupStatus>
    {
        public CommandRequest_GetReleaseGroupStatusMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetReleaseGroupStatus);
        }
    }

    public class CommandRequest_GetReviewsMap : SubclassMap<CommandRequest_GetReviews>
    {
        public CommandRequest_GetReviewsMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetReviews);
        }
    }

    public class CommandRequest_GetAniDBTitlesMap : SubclassMap<CommandRequest_GetAniDBTitles>
    {
        public CommandRequest_GetAniDBTitlesMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetTitles);
        }
    }

    public class CommandRequest_GetUpdatedMap : SubclassMap<CommandRequest_GetUpdated>
    {
        public CommandRequest_GetUpdatedMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_GetUpdated);
        }
    }

    public class CommandRequest_SyncMyListMap : SubclassMap<CommandRequest_SyncMyList>
    {
        public CommandRequest_SyncMyListMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_SyncMyList);
        }
    }

    public class CommandRequest_SyncMyVotesMap : SubclassMap<CommandRequest_SyncMyVotes>
    {
        public CommandRequest_SyncMyVotesMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_SyncVotes);
        }
    }

    public class CommandRequest_UpdateMyListStatsMap : SubclassMap<CommandRequest_UpdateMyListStats>
    {
        public CommandRequest_UpdateMyListStatsMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_UpdateMylistStats);
        }
    }

    public class CommandRequest_UpdateMyListFileStatusMap : SubclassMap<CommandRequest_UpdateMyListFileStatus>
    {
        public CommandRequest_UpdateMyListFileStatusMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_UpdateWatchedUDP);
        }
    }

    public class CommandRequest_VoteAnimeMap : SubclassMap<CommandRequest_VoteAnime>
    {
        public CommandRequest_VoteAnimeMap()
        {
            DiscriminatorValue((int) CommandRequestType.AniDB_VoteAnime);
        }
    }

    public class CommandRequest_Azure_SendAnimeFullMap : SubclassMap<CommandRequest_Azure_SendAnimeFull>
    {
        public CommandRequest_Azure_SendAnimeFullMap()
        {
            DiscriminatorValue((int) CommandRequestType.Azure_SendAnimeFull);
        }
    }

    public class CommandRequest_Azure_SendAnimeTitleMap : SubclassMap<CommandRequest_Azure_SendAnimeTitle>
    {
        public CommandRequest_Azure_SendAnimeTitleMap()
        {
            DiscriminatorValue((int) CommandRequestType.Azure_SendAnimeTitle);
        }
    }

    public class CommandRequest_Azure_SendAnimeXMLMap : SubclassMap<CommandRequest_Azure_SendAnimeXML>
    {
        public CommandRequest_Azure_SendAnimeXMLMap()
        {
            DiscriminatorValue((int) CommandRequestType.Azure_SendAnimeXML);
        }
    }

    public class CommandRequest_Azure_SendUserInfoMap : SubclassMap<CommandRequest_Azure_SendUserInfo>
    {
        public CommandRequest_Azure_SendUserInfoMap()
        {
            DiscriminatorValue((int) CommandRequestType.Azure_SendUserInfo);
        }
    }

    public class CommandRequest_HashFileMap : SubclassMap<CommandRequest_HashFile>
    {
        public CommandRequest_HashFileMap()
        {
            DiscriminatorValue((int) CommandRequestType.HashFile);
        }
    }

    public class CommandRequest_DownloadImageMap : SubclassMap<CommandRequest_DownloadImage>
    {
        public CommandRequest_DownloadImageMap()
        {
            DiscriminatorValue((int) CommandRequestType.ImageDownload);
        }
    }

    public class CommandRequest_LinkAniDBTvDBMap : SubclassMap<CommandRequest_LinkAniDBTvDB>
    {
        public CommandRequest_LinkAniDBTvDBMap()
        {
            DiscriminatorValue((int) CommandRequestType.LinkAniDBTvDB);
        }
    }

    public class CommandRequest_LinkFileManuallyMap : SubclassMap<CommandRequest_LinkFileManually>
    {
        public CommandRequest_LinkFileManuallyMap()
        {
            DiscriminatorValue((int) CommandRequestType.LinkFileManually);
        }
    }

    public class CommandRequest_MALDownloadStatusFromMALMap : SubclassMap<CommandRequest_MALDownloadStatusFromMAL>
    {
        public CommandRequest_MALDownloadStatusFromMALMap()
        {
            DiscriminatorValue((int) CommandRequestType.MAL_DownloadWatchedStates);
        }
    }

    public class CommandRequest_MALSearchAnimeMap : SubclassMap<CommandRequest_MALSearchAnime>
    {
        public CommandRequest_MALSearchAnimeMap()
        {
            DiscriminatorValue((int) CommandRequestType.MAL_SearchAnime);
        }
    }

    public class CommandRequest_MALUpdatedWatchedStatusMap : SubclassMap<CommandRequest_MALUpdatedWatchedStatus>
    {
        public CommandRequest_MALUpdatedWatchedStatusMap()
        {
            DiscriminatorValue((int) CommandRequestType.MAL_UpdateStatus);
        }
    }

    public class CommandRequest_MALUploadStatusToMALMap : SubclassMap<CommandRequest_MALUploadStatusToMAL>
    {
        public CommandRequest_MALUploadStatusToMALMap()
        {
            DiscriminatorValue((int) CommandRequestType.MAL_UploadWatchedStates);
        }
    }

    public class CommandRequest_MovieDBSearchAnimeMap : SubclassMap<CommandRequest_MovieDBSearchAnime>
    {
        public CommandRequest_MovieDBSearchAnimeMap()
        {
            DiscriminatorValue((int) CommandRequestType.MovieDB_SearchAnime);
        }
    }

    public class CommandRequest_PlexSyncWatchedMap : SubclassMap<CommandRequest_PlexSyncWatched>
    {
        public CommandRequest_PlexSyncWatchedMap()
        {
            DiscriminatorValue((int) CommandRequestType.Plex_Sync);
        }
    }

    public class CommandRequest_ProcessFileMap : SubclassMap<CommandRequest_ProcessFile>
    {
        public CommandRequest_ProcessFileMap()
        {
            DiscriminatorValue((int) CommandRequestType.ProcessFile);
        }
    }

    public class CommandRequest_ReadMediaInfoMap : SubclassMap<CommandRequest_ReadMediaInfo>
    {
        public CommandRequest_ReadMediaInfoMap()
        {
            DiscriminatorValue((int) CommandRequestType.ReadMediaInfo);
        }
    }

    public class CommandRequest_RefreshAnimeMap : SubclassMap<CommandRequest_RefreshAnime>
    {
        public CommandRequest_RefreshAnimeMap()
        {
            DiscriminatorValue((int) CommandRequestType.Refresh_AnimeStats);
        }
    }

    public class CommandRequest_RefreshGroupFilterMap : SubclassMap<CommandRequest_RefreshGroupFilter>
    {
        public CommandRequest_RefreshGroupFilterMap()
        {
            DiscriminatorValue((int) CommandRequestType.Refresh_GroupFilter);
        }
    }

    public class CommandRequest_TraktCollectionEpisodeMap : SubclassMap<CommandRequest_TraktCollectionEpisode>
    {
        public CommandRequest_TraktCollectionEpisodeMap()
        {
            DiscriminatorValue((int) CommandRequestType.Trakt_EpisodeCollection);
        }
    }

    public class CommandRequest_TraktHistoryEpisodeMap : SubclassMap<CommandRequest_TraktHistoryEpisode>
    {
        public CommandRequest_TraktHistoryEpisodeMap()
        {
            DiscriminatorValue((int) CommandRequestType.Trakt_EpisodeHistory);
        }
    }

    public class CommandRequest_TraktSearchAnimeMap : SubclassMap<CommandRequest_TraktSearchAnime>
    {
        public CommandRequest_TraktSearchAnimeMap()
        {
            DiscriminatorValue((int) CommandRequestType.Trakt_SearchAnime);
        }
    }

    public class CommandRequest_TraktSyncCollectionMap : SubclassMap<CommandRequest_TraktSyncCollection>
    {
        public CommandRequest_TraktSyncCollectionMap()
        {
            DiscriminatorValue((int) CommandRequestType.Trakt_SyncCollection);
        }
    }

    public class CommandRequest_TraktSyncCollectionSeriesMap : SubclassMap<CommandRequest_TraktSyncCollectionSeries>
    {
        public CommandRequest_TraktSyncCollectionSeriesMap()
        {
            DiscriminatorValue((int) CommandRequestType.Trakt_SyncCollectionSeries);
        }
    }

    public class CommandRequest_TraktUpdateAllSeriesMap : SubclassMap<CommandRequest_TraktUpdateAllSeries>
    {
        public CommandRequest_TraktUpdateAllSeriesMap()
        {
            DiscriminatorValue((int) CommandRequestType.Trakt_UpdateAllSeries);
        }
    }

    public class CommandRequest_TraktUpdateInfoMap : SubclassMap<CommandRequest_TraktUpdateInfo>
    {
        public CommandRequest_TraktUpdateInfoMap()
        {
            DiscriminatorValue((int) CommandRequestType.Trakt_UpdateInfo);
        }
    }

    public class CommandRequest_TvDBDownloadImagesMap : SubclassMap<CommandRequest_TvDBDownloadImages>
    {
        public CommandRequest_TvDBDownloadImagesMap()
        {
            DiscriminatorValue((int) CommandRequestType.TvDB_DownloadImages);
        }
    }

    public class CommandRequest_TvDBSearchAnimeMap : SubclassMap<CommandRequest_TvDBSearchAnime>
    {
        public CommandRequest_TvDBSearchAnimeMap()
        {
            DiscriminatorValue((int) CommandRequestType.TvDB_SearchAnime);
        }
    }

    public class CommandRequest_TvDBUpdateEpisodeMap : SubclassMap<CommandRequest_TvDBUpdateEpisode>
    {
        public CommandRequest_TvDBUpdateEpisodeMap()
        {
            DiscriminatorValue((int) CommandRequestType.TvDB_UpdateEpisode);
        }
    }

    public class CommandRequest_TvDBUpdateSeriesMap : SubclassMap<CommandRequest_TvDBUpdateSeries>
    {
        public CommandRequest_TvDBUpdateSeriesMap()
        {
            DiscriminatorValue((int) CommandRequestType.TvDB_UpdateSeries);
        }
    }

    public class CommandRequest_ValidateAllImagesMap : SubclassMap<CommandRequest_ValidateAllImages>
    {
        public CommandRequest_ValidateAllImagesMap()
        {
            DiscriminatorValue((int) CommandRequestType.ValidateAllImages);
        }
    }

    public class CommandRequest_WebCacheDeleteXRefAniDBMALMap : SubclassMap<CommandRequest_WebCacheDeleteXRefAniDBMAL>
    {
        public CommandRequest_WebCacheDeleteXRefAniDBMALMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_DeleteXRefAniDBMAL);
        }
    }

    public class CommandRequest_WebCacheDeleteXRefAniDBOtherMap : SubclassMap<CommandRequest_WebCacheDeleteXRefAniDBOther>
    {
        public CommandRequest_WebCacheDeleteXRefAniDBOtherMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_DeleteXRefAniDBOther);
        }
    }

    public class CommandRequest_WebCacheDeleteXRefAniDBTraktMap : SubclassMap<CommandRequest_WebCacheDeleteXRefAniDBTrakt>
    {
        public CommandRequest_WebCacheDeleteXRefAniDBTraktMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_DeleteXRefAniDBTrakt);
        }
    }

    public class CommandRequest_WebCacheDeleteXRefAniDBTvDBMap : SubclassMap<CommandRequest_WebCacheDeleteXRefAniDBTvDB>
    {
        public CommandRequest_WebCacheDeleteXRefAniDBTvDBMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_DeleteXRefAniDBTvDB);
        }
    }

    public class CommandRequest_WebCacheDeleteXRefFileEpisodeMap : SubclassMap<CommandRequest_WebCacheDeleteXRefFileEpisode>
    {
        public CommandRequest_WebCacheDeleteXRefFileEpisodeMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_DeleteXRefFileEpisode);
        }
    }

    public class CommandRequest_WebCacheSendXRefAniDBMALMap : SubclassMap<CommandRequest_WebCacheSendXRefAniDBMAL>
    {
        public CommandRequest_WebCacheSendXRefAniDBMALMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_SendXRefAniDBMAL);
        }
    }

    public class CommandRequest_WebCacheSendXRefAniDBOtherMap : SubclassMap<CommandRequest_WebCacheSendXRefAniDBOther>
    {
        public CommandRequest_WebCacheSendXRefAniDBOtherMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_SendXRefAniDBOther);
        }
    }

    public class CommandRequest_WebCacheSendXRefAniDBTraktMap : SubclassMap<CommandRequest_WebCacheSendXRefAniDBTrakt>
    {
        public CommandRequest_WebCacheSendXRefAniDBTraktMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_SendXRefAniDBTrakt);
        }
    }

    public class CommandRequest_WebCacheSendXRefAniDBTvDBMap : SubclassMap<CommandRequest_WebCacheSendXRefAniDBTvDB>
    {
        public CommandRequest_WebCacheSendXRefAniDBTvDBMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_SendXRefAniDBTvDB);
        }
    }

    public class CommandRequest_WebCacheSendXRefFileEpisodeMap : SubclassMap<CommandRequest_WebCacheSendXRefFileEpisode>
    {
        public CommandRequest_WebCacheSendXRefFileEpisodeMap()
        {
            DiscriminatorValue((int) CommandRequestType.WebCache_SendXRefFileEpisode);
        }
    }

}