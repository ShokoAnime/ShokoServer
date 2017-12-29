namespace Shoko.Server.Commands
{
    public class CommandHelper
    {
        // List of priorities for commands
        // Order is as such:
        //    Get Max Priority
        //    Get/Update
        //    Set Internal
        //    Recalculate Internal (stats, contracts, etc)
        //    Sync External
        //    Set External
        //
        // Pri 1
        //------
        // Reserved for commands user manually initiates from UI
        //------
        // Pri 2
        //------
        // CommandRequest_GetAnimeHTTP
        //------
        // Pri 3
        //------
        // CommandRequest_ProcessFile
        // CommandRequest_GetFile
        // CommandRequest_LinkFileManually
        //------
        // Pri 4
        //------
        // CommandRequest_GetUpdated
        // CommandRequest_GetEpsode
        //------
        // Pri 5
        //------
        // CommandRequest_GetCalendar
        // CommandRequest_GetReleaseGroup
        // CommandRequest_GetReleaseGroupStatus
        // CommandRequest_GetReviews
        // CommandRequest_LinkAniDBTvDB
        //------
        // Pri 6
        //------
        // CommandRequest_AddFileToMyList #This also updates watched state from AniDB, so it has priority
        // CommandRequest_GetMyListFileStatus
        // CommandRequest_MALDownloadStatusFromMAL
        // CommandRequest_MALSearchAnime
        // CommandRequest_MALUpdatedWatchedStatus
        // CommandRequest_MovieDBSearchAnime
        // CommandRequest_TraktSearchAnime
        // CommandRequest_TraktUpdateAllSeries
        // CommandRequest_TraktUpdateInfo
        // CommandRequest_TvDBSearchAnime
        // CommandRequest_TvDBUpdateEpisodes
        // CommandRequest_TvDBUpdateSeries
        // CommandRequest_UpdateMyListFileStatus
        // CommandRequest_VoteAnime
        //------
        // Pri 7
        //------
        // CommandRequest_PlexSyncWatched
        // CommandRequest_SyncMyList
        // CommandRequest_SyncMyVotes
        // CommandRequest_TraktShowEpisodeUnseen
        // CommandRequest_TraktSyncCollection
        // CommandRequest_TraktSyncCollectionSeries
        // CommandRequest_UpdateMylistStats
        //------
        // Pri 8
        //------
        // CommandRequest_RefreshAnime
        //------
        // Pri 9
        //------
        // CommandRequest_RefreshGroupFilter
        //------
        // Pri 10
        //------
        // CommandRequest_Azure_SendAnimeFull
        // CommandRequest_Azure_SendAnimeTitle
        // CommandRequest_Azure_SendAnimeXML
        // CommandRequest_DeleteFileFromMyList
        // CommandRequest_MALUploadStatusToMAL
        // CommandRequest_WebCacheDeleteXRefAniDBOther
        // CommandRequest_WebCacheDeleteXRefAniDBTrakt
        // CommandRequest_WebCacheDeleteXRefAniDBTvDB
        // CommandRequest_WebCacheDeleteXRefAniDBTvDBAll
        // CommandRequest_WebCacheDeleteXRefFileEpisode
        // CommandRequest_WebCacheSendAniDB_File
        // CommandRequest_WebCacheSendFileHash
        // CommandRequest_WebCacheSendXRefAniDBOther
        // CommandRequest_WebCacheSendXRefAniDBTrakt
        // CommandRequest_WebCacheSendXRefAniDBTvDB
        // CommandRequest_WebCacheSendXRefFileEpisode
        //------
        // Pri 11
        //------

        public static CommandRequest GetCommand(CommandRequest crdb)
        {
            CommandRequestType crt = (CommandRequestType) crdb.CommandType;
            switch (crt)
            {
                case CommandRequestType.AniDB_AddFileUDP:
                    CommandRequest_AddFileToMyList cr_af = new CommandRequest_AddFileToMyList();
                    cr_af.InitFromDB(crdb);
                    return cr_af;

                case CommandRequestType.AniDB_DeleteFileUDP:
                    CommandRequest_DeleteFileFromMyList cr_AniDB_DeleteFileUDP =
                        new CommandRequest_DeleteFileFromMyList();
                    cr_AniDB_DeleteFileUDP.InitFromDB(crdb);
                    return cr_AniDB_DeleteFileUDP;

                case CommandRequestType.AniDB_GetAnimeHTTP:
                    CommandRequest_GetAnimeHTTP cr_geth = new CommandRequest_GetAnimeHTTP();
                    cr_geth.InitFromDB(crdb);
                    return cr_geth;

                case CommandRequestType.AniDB_GetCalendar:
                    CommandRequest_GetCalendar cr_GetCalendar = new CommandRequest_GetCalendar();
                    cr_GetCalendar.InitFromDB(crdb);
                    return cr_GetCalendar;

                case CommandRequestType.AniDB_GetEpisodeUDP:
                    CommandRequest_GetEpisode cr_CommandRequest_GetEpisode = new CommandRequest_GetEpisode();
                    cr_CommandRequest_GetEpisode.InitFromDB(crdb);
                    return cr_CommandRequest_GetEpisode;

                case CommandRequestType.AniDB_GetFileUDP:
                    CommandRequest_GetFile cr_AniDB_GetFileUDP = new CommandRequest_GetFile();
                    cr_AniDB_GetFileUDP.InitFromDB(crdb);
                    return cr_AniDB_GetFileUDP;

                case CommandRequestType.AniDB_GetMyListFile:
                    CommandRequest_GetFileMyListStatus cr_MyListStatus = new CommandRequest_GetFileMyListStatus();
                    cr_MyListStatus.InitFromDB(crdb);
                    return cr_MyListStatus;

                case CommandRequestType.AniDB_GetReleaseGroup:
                    CommandRequest_GetReleaseGroup cr_GetReleaseGroup = new CommandRequest_GetReleaseGroup();
                    cr_GetReleaseGroup.InitFromDB(crdb);
                    return cr_GetReleaseGroup;

                case CommandRequestType.AniDB_GetReleaseGroupStatus:
                    CommandRequest_GetReleaseGroupStatus cr_GetReleaseGroupStatus =
                        new CommandRequest_GetReleaseGroupStatus();
                    cr_GetReleaseGroupStatus.InitFromDB(crdb);
                    return cr_GetReleaseGroupStatus;

                case CommandRequestType.AniDB_GetReviews:
                    CommandRequest_GetReviews cr_GetReviews = new CommandRequest_GetReviews();
                    cr_GetReviews.InitFromDB(crdb);
                    return cr_GetReviews;

                case CommandRequestType.AniDB_GetTitles:
                    CommandRequest_GetAniDBTitles cr_CommandRequest_GetAniDBTitles =
                        new CommandRequest_GetAniDBTitles();
                    cr_CommandRequest_GetAniDBTitles.InitFromDB(crdb);
                    return cr_CommandRequest_GetAniDBTitles;

                case CommandRequestType.AniDB_GetUpdated:
                    CommandRequest_GetUpdated cr_GetUpdated = new CommandRequest_GetUpdated();
                    cr_GetUpdated.InitFromDB(crdb);
                    return cr_GetUpdated;

                case CommandRequestType.AniDB_SyncMyList:
                    CommandRequest_SyncMyList cr_SyncMyList = new CommandRequest_SyncMyList();
                    cr_SyncMyList.InitFromDB(crdb);
                    return cr_SyncMyList;

                case CommandRequestType.AniDB_SyncVotes:
                    CommandRequest_SyncMyVotes cr_SyncVotes = new CommandRequest_SyncMyVotes();
                    cr_SyncVotes.InitFromDB(crdb);
                    return cr_SyncVotes;

                case CommandRequestType.AniDB_UpdateMylistStats:
                    CommandRequest_UpdateMyListStats crAniDbUpdateMyListStats =
                        new CommandRequest_UpdateMyListStats();
                    crAniDbUpdateMyListStats.InitFromDB(crdb);
                    return crAniDbUpdateMyListStats;

                case CommandRequestType.AniDB_UpdateWatchedUDP:
                    CommandRequest_UpdateMyListFileStatus cr_umlf = new CommandRequest_UpdateMyListFileStatus();
                    cr_umlf.InitFromDB(crdb);
                    return cr_umlf;

                case CommandRequestType.AniDB_VoteAnime:
                    CommandRequest_VoteAnime cr_VoteAnime = new CommandRequest_VoteAnime();
                    cr_VoteAnime.InitFromDB(crdb);
                    return cr_VoteAnime;

                case CommandRequestType.Azure_SendAnimeFull:
                    CommandRequest_Azure_SendAnimeFull cr_CommandRequest_Azure_SendAnimeFull =
                        new CommandRequest_Azure_SendAnimeFull();
                    cr_CommandRequest_Azure_SendAnimeFull.InitFromDB(crdb);
                    return cr_CommandRequest_Azure_SendAnimeFull;

                case CommandRequestType.Azure_SendAnimeTitle:
                    CommandRequest_Azure_SendAnimeTitle cr_CommandRequest_Azure_SendAnimeTitle =
                        new CommandRequest_Azure_SendAnimeTitle();
                    cr_CommandRequest_Azure_SendAnimeTitle.InitFromDB(crdb);
                    return cr_CommandRequest_Azure_SendAnimeTitle;

                case CommandRequestType.Azure_SendAnimeXML:
                    CommandRequest_Azure_SendAnimeXML cr_CommandRequest_Azure_SendAnimeXML =
                        new CommandRequest_Azure_SendAnimeXML();
                    cr_CommandRequest_Azure_SendAnimeXML.InitFromDB(crdb);
                    return cr_CommandRequest_Azure_SendAnimeXML;

                case CommandRequestType.Azure_SendUserInfo:
                    CommandRequest_Azure_SendUserInfo cr_CommandRequest_Azure_SendUserInfo =
                        new CommandRequest_Azure_SendUserInfo();
                    cr_CommandRequest_Azure_SendUserInfo.InitFromDB(crdb);
                    return cr_CommandRequest_Azure_SendUserInfo;

                case CommandRequestType.HashFile:
                    CommandRequest_HashFile cr_HashFile = new CommandRequest_HashFile();
                    cr_HashFile.InitFromDB(crdb);
                    return cr_HashFile;

                case CommandRequestType.ImageDownload:
                    CommandRequest_DownloadImage cr_ImageDownload = new CommandRequest_DownloadImage();
                    cr_ImageDownload.InitFromDB(crdb);
                    return cr_ImageDownload;

                case CommandRequestType.LinkAniDBTvDB:
                    CommandRequest_LinkAniDBTvDB cr_linkAniDBTvDB = new CommandRequest_LinkAniDBTvDB();
                    cr_linkAniDBTvDB.InitFromDB(crdb);
                    return cr_linkAniDBTvDB;

                case CommandRequestType.LinkFileManually:
                    CommandRequest_LinkFileManually cr_LinkFile = new CommandRequest_LinkFileManually();
                    cr_LinkFile.InitFromDB(crdb);
                    return cr_LinkFile;

                case CommandRequestType.MAL_DownloadWatchedStates:
                    CommandRequest_MALDownloadStatusFromMAL cr_MAL_DownloadWatchedStates =
                        new CommandRequest_MALDownloadStatusFromMAL();
                    cr_MAL_DownloadWatchedStates.InitFromDB(crdb);
                    return cr_MAL_DownloadWatchedStates;

                case CommandRequestType.MAL_SearchAnime:
                    CommandRequest_MALSearchAnime cr_MAL_SearchAnime = new CommandRequest_MALSearchAnime();
                    cr_MAL_SearchAnime.InitFromDB(crdb);
                    return cr_MAL_SearchAnime;

                case CommandRequestType.MAL_UpdateStatus:
                    CommandRequest_MALUpdatedWatchedStatus cr_MAL_UpdateStatus =
                        new CommandRequest_MALUpdatedWatchedStatus();
                    cr_MAL_UpdateStatus.InitFromDB(crdb);
                    return cr_MAL_UpdateStatus;

                case CommandRequestType.MAL_UploadWatchedStates:
                    CommandRequest_MALUploadStatusToMAL cr_MAL_UploadWatchedStates =
                        new CommandRequest_MALUploadStatusToMAL();
                    cr_MAL_UploadWatchedStates.InitFromDB(crdb);
                    return cr_MAL_UploadWatchedStates;

                case CommandRequestType.MovieDB_SearchAnime:
                    CommandRequest_MovieDBSearchAnime cr_MovieDB_SearchAnime = new CommandRequest_MovieDBSearchAnime();
                    cr_MovieDB_SearchAnime.InitFromDB(crdb);
                    return cr_MovieDB_SearchAnime;

                case CommandRequestType.Plex_Sync:
                    CommandRequest_PlexSyncWatched cr_PlexSync = new CommandRequest_PlexSyncWatched();
                    cr_PlexSync.InitFromDB(crdb);
                    return cr_PlexSync;

                case CommandRequestType.ProcessFile:
                    CommandRequest_ProcessFile cr_pf = new CommandRequest_ProcessFile();
                    cr_pf.InitFromDB(crdb);
                    return cr_pf;

                case CommandRequestType.ReadMediaInfo:
                    CommandRequest_ReadMediaInfo cr_ReadMediaInfo = new CommandRequest_ReadMediaInfo();
                    cr_ReadMediaInfo.InitFromDB(crdb);
                    return cr_ReadMediaInfo;

                case CommandRequestType.Refresh_AnimeStats:
                    CommandRequest_RefreshAnime cr_refreshAnime = new CommandRequest_RefreshAnime();
                    cr_refreshAnime.InitFromDB(crdb);
                    return cr_refreshAnime;

                case CommandRequestType.Refresh_GroupFilter:
                    CommandRequest_RefreshGroupFilter cr_refreshGroupFilter = new CommandRequest_RefreshGroupFilter();
                    cr_refreshGroupFilter.InitFromDB(crdb);
                    return cr_refreshGroupFilter;

                case CommandRequestType.Trakt_EpisodeCollection:
                    CommandRequest_TraktCollectionEpisode cr_TraktCollectionEpisode =
                        new CommandRequest_TraktCollectionEpisode();
                    cr_TraktCollectionEpisode.InitFromDB(crdb);
                    return cr_TraktCollectionEpisode;

                case CommandRequestType.Trakt_EpisodeHistory:
                    CommandRequest_TraktHistoryEpisode cr_Trakt_EpisodeHistory =
                        new CommandRequest_TraktHistoryEpisode();
                    cr_Trakt_EpisodeHistory.InitFromDB(crdb);
                    return cr_Trakt_EpisodeHistory;

                case CommandRequestType.Trakt_SearchAnime:
                    CommandRequest_TraktSearchAnime cr_Trakt_SearchAnime = new CommandRequest_TraktSearchAnime();
                    cr_Trakt_SearchAnime.InitFromDB(crdb);
                    return cr_Trakt_SearchAnime;

                case CommandRequestType.Trakt_SyncCollection:
                    CommandRequest_TraktSyncCollection cr_Trakt_SyncCollection =
                        new CommandRequest_TraktSyncCollection();
                    cr_Trakt_SyncCollection.InitFromDB(crdb);
                    return cr_Trakt_SyncCollection;

                case CommandRequestType.Trakt_SyncCollectionSeries:
                    CommandRequest_TraktSyncCollectionSeries cr_CommandRequest_TraktSyncCollectionSeries =
                        new CommandRequest_TraktSyncCollectionSeries();
                    cr_CommandRequest_TraktSyncCollectionSeries.InitFromDB(crdb);
                    return cr_CommandRequest_TraktSyncCollectionSeries;


                case CommandRequestType.Trakt_UpdateAllSeries:
                    CommandRequest_TraktUpdateAllSeries cr_Trakt_UpdateAllSeries =
                        new CommandRequest_TraktUpdateAllSeries();
                    cr_Trakt_UpdateAllSeries.InitFromDB(crdb);
                    return cr_Trakt_UpdateAllSeries;

                case CommandRequestType.Trakt_UpdateInfo:
                    CommandRequest_TraktUpdateInfo cr_Trakt_UpdateInfoImages =
                        new CommandRequest_TraktUpdateInfo();
                    cr_Trakt_UpdateInfoImages.InitFromDB(crdb);
                    return cr_Trakt_UpdateInfoImages;

                case CommandRequestType.TvDB_DownloadImages:
                    CommandRequest_TvDBDownloadImages cr_TvDB_DownloadImages = new CommandRequest_TvDBDownloadImages();
                    cr_TvDB_DownloadImages.InitFromDB(crdb);
                    return cr_TvDB_DownloadImages;

                case CommandRequestType.TvDB_SearchAnime:
                    CommandRequest_TvDBSearchAnime cr_TvDB_SearchAnime = new CommandRequest_TvDBSearchAnime();
                    cr_TvDB_SearchAnime.InitFromDB(crdb);
                    return cr_TvDB_SearchAnime;

                case CommandRequestType.TvDB_UpdateEpisode:
                    CommandRequest_TvDBUpdateEpisode cr_TvDB_Episode =
                        new CommandRequest_TvDBUpdateEpisode();
                    cr_TvDB_Episode.InitFromDB(crdb);
                    return cr_TvDB_Episode;

                case CommandRequestType.TvDB_UpdateSeries:
                    CommandRequest_TvDBUpdateSeries cr_TvDB_Episodes =
                        new CommandRequest_TvDBUpdateSeries();
                    cr_TvDB_Episodes.InitFromDB(crdb);
                    return cr_TvDB_Episodes;

                case CommandRequestType.ValidateAllImages:
                    CommandRequest_ValidateAllImages cr_ValidateImages = new CommandRequest_ValidateAllImages();
                    cr_ValidateImages.InitFromDB(crdb);
                    return cr_ValidateImages;

                case CommandRequestType.WebCache_DeleteXRefAniDBMAL:
                    CommandRequest_WebCacheDeleteXRefAniDBMAL cr_WebCacheDeleteXRefAniDBMAL =
                        new CommandRequest_WebCacheDeleteXRefAniDBMAL();
                    cr_WebCacheDeleteXRefAniDBMAL.InitFromDB(crdb);
                    return cr_WebCacheDeleteXRefAniDBMAL;

                case CommandRequestType.WebCache_DeleteXRefAniDBOther:
                    CommandRequest_WebCacheDeleteXRefAniDBOther cr_SendXRefAniDBOther =
                        new CommandRequest_WebCacheDeleteXRefAniDBOther();
                    cr_SendXRefAniDBOther.InitFromDB(crdb);
                    return cr_SendXRefAniDBOther;

                case CommandRequestType.WebCache_DeleteXRefAniDBTrakt:
                    CommandRequest_WebCacheDeleteXRefAniDBTrakt cr_WebCache_DeleteXRefAniDBTrakt =
                        new CommandRequest_WebCacheDeleteXRefAniDBTrakt();
                    cr_WebCache_DeleteXRefAniDBTrakt.InitFromDB(crdb);
                    return cr_WebCache_DeleteXRefAniDBTrakt;

                case CommandRequestType.WebCache_DeleteXRefAniDBTvDB:
                    CommandRequest_WebCacheDeleteXRefAniDBTvDB cr_DeleteXRefAniDBTvDB =
                        new CommandRequest_WebCacheDeleteXRefAniDBTvDB();
                    cr_DeleteXRefAniDBTvDB.InitFromDB(crdb);
                    return cr_DeleteXRefAniDBTvDB;

                case CommandRequestType.WebCache_DeleteXRefFileEpisode:
                    CommandRequest_WebCacheDeleteXRefFileEpisode cr_DeleteXRefFileEpisode =
                        new CommandRequest_WebCacheDeleteXRefFileEpisode();
                    cr_DeleteXRefFileEpisode.InitFromDB(crdb);
                    return cr_DeleteXRefFileEpisode;

                case CommandRequestType.WebCache_SendXRefAniDBMAL:
                    CommandRequest_WebCacheSendXRefAniDBMAL cr_WebCacheSendXRefAniDBMAL =
                        new CommandRequest_WebCacheSendXRefAniDBMAL();
                    cr_WebCacheSendXRefAniDBMAL.InitFromDB(crdb);
                    return cr_WebCacheSendXRefAniDBMAL;

                case CommandRequestType.WebCache_SendXRefAniDBOther:
                    CommandRequest_WebCacheSendXRefAniDBOther cr_WebCacheSendXRefAniDBOther =
                        new CommandRequest_WebCacheSendXRefAniDBOther();
                    cr_WebCacheSendXRefAniDBOther.InitFromDB(crdb);
                    return cr_WebCacheSendXRefAniDBOther;

                case CommandRequestType.WebCache_SendXRefAniDBTrakt:
                    CommandRequest_WebCacheSendXRefAniDBTrakt cr_WebCache_SendXRefAniDBTrakt =
                        new CommandRequest_WebCacheSendXRefAniDBTrakt();
                    cr_WebCache_SendXRefAniDBTrakt.InitFromDB(crdb);
                    return cr_WebCache_SendXRefAniDBTrakt;

                case CommandRequestType.WebCache_SendXRefAniDBTvDB:
                    CommandRequest_WebCacheSendXRefAniDBTvDB cr_SendXRefAniDBTvDB =
                        new CommandRequest_WebCacheSendXRefAniDBTvDB();
                    cr_SendXRefAniDBTvDB.InitFromDB(crdb);
                    return cr_SendXRefAniDBTvDB;

                case CommandRequestType.WebCache_SendXRefFileEpisode:
                    CommandRequest_WebCacheSendXRefFileEpisode cr_SendXRefFileEpisode =
                        new CommandRequest_WebCacheSendXRefFileEpisode();
                    cr_SendXRefFileEpisode.InitFromDB(crdb);
                    return cr_SendXRefFileEpisode;
            }

            return null;
        }
    }
}