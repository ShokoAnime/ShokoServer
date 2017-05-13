using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Azure;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Commands.TvDB;
using Shoko.Server.Commands.WebCache;

namespace Shoko.Server.Commands
{

    public struct QueueStateStruct
    {
        public QueueStateEnum queueState;
        public string[] extraParams;

        public string formatMessage()
        {
            string formatString = getFormatString(queueState);
            return string.Format(formatString, extraParams);
        }

        private string getFormatString(QueueStateEnum id)
        {
            switch (id)
            {
                case QueueStateEnum.AnimeInfo:
                    return Commons.Properties.Resources.Command_AnimeInfo;
                case QueueStateEnum.DeleteError:
                    return Commons.Properties.Resources.Command_DeleteError;
                case QueueStateEnum.DownloadImage:
                    return Commons.Properties.Resources.Command_DownloadImage;
                case QueueStateEnum.DownloadMalWatched:
                    return Commons.Properties.Resources.Command_DownloadMalWatched;
                case QueueStateEnum.DownloadTvDBImages:
                    return Commons.Properties.Resources.Command_DownloadTvDBImages;
                case QueueStateEnum.FileInfo:
                    return Commons.Properties.Resources.Command_FileInfo;
                case QueueStateEnum.GetCalendar:
                    return Commons.Properties.Resources.Command_GetCalendar;
                case QueueStateEnum.GetEpisodeList:
                    return Commons.Properties.Resources.Command_GetEpisodeList;
                case QueueStateEnum.GetFileInfo:
                    return Commons.Properties.Resources.Command_GetFileInfo;
                case QueueStateEnum.GetReleaseGroup:
                    return Commons.Properties.Resources.Command_GetReleaseGroup;
                case QueueStateEnum.GetReleaseInfo:
                    return Commons.Properties.Resources.Command_GetReleaseInfo;
                case QueueStateEnum.GetReviewInfo:
                    return Commons.Properties.Resources.Command_GetReviewInfo;
                case QueueStateEnum.GettingTvDB:
                    return Commons.Properties.Resources.Command_GettingTvDB;
                case QueueStateEnum.GetUpdatedAnime:
                    return Commons.Properties.Resources.Command_GetUpdatedAnime;
                case QueueStateEnum.HashingFile:
                    return Commons.Properties.Resources.Command_HashingFile;
                case QueueStateEnum.CheckingFile:
                    return Commons.Properties.Resources.Command_CheckingFile;
                case QueueStateEnum.Idle:
                    return Commons.Properties.Resources.Command_Idle;
                case QueueStateEnum.Paused:
                    return Commons.Properties.Resources.Command_Paused;
                case QueueStateEnum.Queued:
                    return Commons.Properties.Resources.Command_Queued;
                case QueueStateEnum.ReadingMedia:
                    return Commons.Properties.Resources.Command_ReadingMedia;
                case QueueStateEnum.Refresh:
                    return Commons.Properties.Resources.Command_Refresh;
                case QueueStateEnum.SearchMal:
                    return Commons.Properties.Resources.Command_SearchMal;
                case QueueStateEnum.SearchTMDb:
                    return Commons.Properties.Resources.Command_SearchTMDb;
                case QueueStateEnum.SearchTrakt:
                    return Commons.Properties.Resources.Command_SearchTrakt;
                case QueueStateEnum.SearchTvDB:
                    return Commons.Properties.Resources.Command_SearchTvDB;
                case QueueStateEnum.SendAnimeAzure:
                    return Commons.Properties.Resources.Command_SendAnimeAzure;
                case QueueStateEnum.SendAnimeFull:
                    return Commons.Properties.Resources.Command_SendAnimeFull;
                case QueueStateEnum.SendAnimeTitle:
                    return Commons.Properties.Resources.Command_SendAnimeTitle;
                case QueueStateEnum.SendAnonymousData:
                    return Commons.Properties.Resources.Command_SendAnonymousData;
                case QueueStateEnum.StartingGeneral:
                    return Commons.Properties.Resources.Command_StartingGeneral;
                case QueueStateEnum.StartingHasher:
                    return Commons.Properties.Resources.Command_StartingHasher;
                case QueueStateEnum.StartingImages:
                    return Commons.Properties.Resources.Command_StartingImages;
                case QueueStateEnum.SyncMyList:
                    return Commons.Properties.Resources.Command_SyncMyList;
                case QueueStateEnum.SyncTrakt:
                    return Commons.Properties.Resources.Command_SyncTrakt;
                case QueueStateEnum.SyncTraktEpisodes:
                    return Commons.Properties.Resources.Command_SyncTraktEpisodes;
                case QueueStateEnum.SyncTraktSeries:
                    return Commons.Properties.Resources.Command_SyncTraktSeries;
                case QueueStateEnum.SyncVotes:
                    return Commons.Properties.Resources.Command_SyncVotes;
                case QueueStateEnum.TraktAddHistory:
                    return Commons.Properties.Resources.Command_TraktAddHistory;
                case QueueStateEnum.UpdateMALWatched:
                    return Commons.Properties.Resources.Command_UpdateMALWatched;
                case QueueStateEnum.UpdateMyListInfo:
                    return Commons.Properties.Resources.Command_UpdateMyListInfo;
                case QueueStateEnum.UpdateMyListStats:
                    return Commons.Properties.Resources.Command_UpdateMyListStats;
                case QueueStateEnum.UpdateTrakt:
                    return Commons.Properties.Resources.Command_UpdateTrakt;
                case QueueStateEnum.UpdateTraktData:
                    return Commons.Properties.Resources.Command_UpdateTraktData;
                case QueueStateEnum.UploadMALWatched:
                    return Commons.Properties.Resources.Command_UploadMALWatched;
                case QueueStateEnum.VoteAnime:
                    return Commons.Properties.Resources.Command_VoteAnime;
                case QueueStateEnum.WebCacheDeleteXRefAniDBMAL:
                    return Commons.Properties.Resources.Command_WebCacheDeleteXRefAniDBMAL;
                case QueueStateEnum.WebCacheDeleteXRefAniDBOther:
                    return Commons.Properties.Resources.Command_WebCacheDeleteXRefAniDBOther;
                case QueueStateEnum.WebCacheDeleteXRefAniDBTrakt:
                    return Commons.Properties.Resources.Command_WebCacheDeleteXRefAniDBTrakt;
                case QueueStateEnum.WebCacheDeleteXRefAniDBTvDB:
                    return Commons.Properties.Resources.Command_WebCacheDeleteXRefAniDBTvDB;
                case QueueStateEnum.WebCacheDeleteXRefFileEpisode:
                    return Commons.Properties.Resources.Command_WebCacheDeleteXRefFileEpisode;
                case QueueStateEnum.WebCacheSendXRefAniDBMAL:
                    return Commons.Properties.Resources.Command_WebCacheSendXRefAniDBMAL;
                case QueueStateEnum.WebCacheSendXRefAniDBOther:
                    return Commons.Properties.Resources.Command_WebCacheSendXRefAniDBOther;
                case QueueStateEnum.WebCacheSendXRefAniDBTrakt:
                    return Commons.Properties.Resources.Command_WebCacheSendXRefAniDBTrakt;
                case QueueStateEnum.WebCacheSendXRefAniDBTvDB:
                    return Commons.Properties.Resources.Command_WebCacheSendXRefAniDBTvDB;
                case QueueStateEnum.WebCacheSendXRefFileEpisode:
                    return Commons.Properties.Resources.Command_WebCacheSendXRefFileEpisode;
                case QueueStateEnum.AniDB_MyListAdd:
                    return Commons.Properties.Resources.AniDB_MyListAdd;
                case QueueStateEnum.AniDB_MyListDelete:
                    return Commons.Properties.Resources.AniDB_MyListDelete;
                case QueueStateEnum.AniDB_GetTitles:
                    return Commons.Properties.Resources.AniDB_GetTitles;
                case QueueStateEnum.Actions_SyncVotes:
                    return Commons.Properties.Resources.Actions_SyncVotes;
                case QueueStateEnum.LinkAniDBTvDB:
                    return Commons.Properties.Resources.Command_LinkAniDBTvDB;
                case QueueStateEnum.RefreshGroupFilter:
                    return Commons.Properties.Resources.Command_RefreshGroupFilter;
                case QueueStateEnum.SyncPlex:
                    return Commons.Properties.Resources.Command_SyncPlex;
                case QueueStateEnum.LinkFileManually:
                    return Commons.Properties.Resources.Command_LinkFileManually;
                default:
                    throw new System.Exception("Unknown queue state format string");
                    ;
            }
        }
    }

    public class CommandHelper
    {
        // List of Default priorities for commands
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
        //------
        // Pri 4
        //------
        // CommandRequest_GetUpdated
        // CommandRequest_ReadMediaInfo
        // CommandRequest_GetEpsode
        //------
        // Pri 5
        //------
        // CommandRequest_GetReleaseGroupStatus
        //------
        // Pri 6
        //------
        // CommandRequest_SyncMyList
        // CommandRequest_SyncMyVotes
        //------
        // Pri 7
        //------
        // CommandRequest_GetCalendar
        //------
        // Pri 8
        //------
        // CommandRequest_UpdateMyListFileStatus
        // CommandRequest_GetCharactersCreators
        // CommandRequest_TraktSyncCollection
        // CommandRequest_TvDBUpdateSeriesAndEpisodes
        // CommandRequest_TvDBDownloadImages
        // CommandRequest_TvDBSearchAnime
        // CommandRequest_MovieDBSearchAnime
        // CommandRequest_TraktSearchAnime
        // CommandRequest_MALSearchAnime
        // CommandRequest_LinkFileManually
        //------
        // Pri 9
        //------
        // CommandRequest_WebCacheSendFileHash
        // CommandRequest_GetReviews
        // CommandRequest_GetReleaseGroup
        // CommandRequest_WebCacheSendXRefFileEpisode
        // CommandRequest_WebCacheDeleteXRefFileEpisode
        // CommandRequest_AddFileToMyList
        // CommandRequest_DeleteFileFromMyList
        // CommandRequest_VoteAnime
        // CommandRequest_WebCacheDeleteXRefAniDBTvDB
        // CommandRequest_WebCacheDeleteXRefAniDBTvDBAll
        // CommandRequest_WebCacheSendXRefAniDBTvDB
        // CommandRequest_WebCacheSendXRefAniDBOther
        // CommandRequest_WebCacheDeleteXRefAniDBOther
        // CommandRequest_WebCacheDeleteXRefAniDBTrakt
        // CommandRequest_WebCacheSendXRefAniDBTrakt
        // CommandRequest_TraktUpdateInfoAndImages
        // CommandRequest_TraktSyncCollectionSeries
        // CommandRequest_TraktShowEpisodeUnseen
        // CommandRequest_DownloadImage
        // CommandRequest_TraktUpdateAllSeries
        // CommandRequest_MALUpdatedWatchedStatus
        // CommandRequest_MALUploadStatusToMAL
        // CommandRequest_MALDownloadStatusFromMAL
        // CommandRequest_WebCacheSendAniDB_File
        // CommandRequest_Azure_SendAnimeFull
        //------
        // Pri 10
        //------
        // CommandRequest_UpdateMylistStats
        // CommandRequest_Azure_SendAnimeXML
        //------
        // Pri 11
        //------
        // CommandRequest_Azure_SendAnimeTitle

        public static ICommandRequest GetCommand(CommandRequest crdb)
        {
            CommandRequestType crt = (CommandRequestType) crdb.CommandType;
            switch (crt)
            {
                case CommandRequestType.Trakt_SyncCollectionSeries:
                    CommandRequest_TraktSyncCollectionSeries cr_CommandRequest_TraktSyncCollectionSeries =
                        new CommandRequest_TraktSyncCollectionSeries();
                    cr_CommandRequest_TraktSyncCollectionSeries.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_TraktSyncCollectionSeries;

                case CommandRequestType.AniDB_GetEpisodeUDP:
                    CommandRequest_GetEpisode cr_CommandRequest_GetEpisode = new CommandRequest_GetEpisode();
                    cr_CommandRequest_GetEpisode.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_GetEpisode;

                case CommandRequestType.Azure_SendAnimeTitle:
                    CommandRequest_Azure_SendAnimeTitle cr_CommandRequest_Azure_SendAnimeTitle =
                        new CommandRequest_Azure_SendAnimeTitle();
                    cr_CommandRequest_Azure_SendAnimeTitle.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendAnimeTitle;

                case CommandRequestType.AniDB_GetTitles:
                    CommandRequest_GetAniDBTitles cr_CommandRequest_GetAniDBTitles =
                        new CommandRequest_GetAniDBTitles();
                    cr_CommandRequest_GetAniDBTitles.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_GetAniDBTitles;

                case CommandRequestType.Azure_SendAnimeXML:
                    CommandRequest_Azure_SendAnimeXML cr_CommandRequest_Azure_SendAnimeXML =
                        new CommandRequest_Azure_SendAnimeXML();
                    cr_CommandRequest_Azure_SendAnimeXML.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendAnimeXML;

                case CommandRequestType.Azure_SendAnimeFull:
                    CommandRequest_Azure_SendAnimeFull cr_CommandRequest_Azure_SendAnimeFull =
                        new CommandRequest_Azure_SendAnimeFull();
                    cr_CommandRequest_Azure_SendAnimeFull.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendAnimeFull;

                case CommandRequestType.Azure_SendUserInfo:
                    CommandRequest_Azure_SendUserInfo cr_CommandRequest_Azure_SendUserInfo =
                        new CommandRequest_Azure_SendUserInfo();
                    cr_CommandRequest_Azure_SendUserInfo.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendUserInfo;

                case CommandRequestType.AniDB_UpdateMylistStats:
                    CommandRequest_UpdateMylistStats cr_AniDB_UpdateMylistStats =
                        new CommandRequest_UpdateMylistStats();
                    cr_AniDB_UpdateMylistStats.LoadFromDBCommand(crdb);
                    return cr_AniDB_UpdateMylistStats;

                case CommandRequestType.MAL_DownloadWatchedStates:
                    CommandRequest_MALDownloadStatusFromMAL cr_MAL_DownloadWatchedStates =
                        new CommandRequest_MALDownloadStatusFromMAL();
                    cr_MAL_DownloadWatchedStates.LoadFromDBCommand(crdb);
                    return cr_MAL_DownloadWatchedStates;

                case CommandRequestType.MAL_UploadWatchedStates:
                    CommandRequest_MALUploadStatusToMAL cr_MAL_UploadWatchedStates =
                        new CommandRequest_MALUploadStatusToMAL();
                    cr_MAL_UploadWatchedStates.LoadFromDBCommand(crdb);
                    return cr_MAL_UploadWatchedStates;

                case CommandRequestType.MAL_UpdateStatus:
                    CommandRequest_MALUpdatedWatchedStatus cr_MAL_UpdateStatus =
                        new CommandRequest_MALUpdatedWatchedStatus();
                    cr_MAL_UpdateStatus.LoadFromDBCommand(crdb);
                    return cr_MAL_UpdateStatus;

                case CommandRequestType.MAL_SearchAnime:
                    CommandRequest_MALSearchAnime cr_MAL_SearchAnime = new CommandRequest_MALSearchAnime();
                    cr_MAL_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_MAL_SearchAnime;

                case CommandRequestType.WebCache_SendXRefAniDBMAL:
                    CommandRequest_WebCacheSendXRefAniDBMAL cr_WebCacheSendXRefAniDBMAL =
                        new CommandRequest_WebCacheSendXRefAniDBMAL();
                    cr_WebCacheSendXRefAniDBMAL.LoadFromDBCommand(crdb);
                    return cr_WebCacheSendXRefAniDBMAL;

                case CommandRequestType.WebCache_DeleteXRefAniDBMAL:
                    CommandRequest_WebCacheDeleteXRefAniDBMAL cr_WebCacheDeleteXRefAniDBMAL =
                        new CommandRequest_WebCacheDeleteXRefAniDBMAL();
                    cr_WebCacheDeleteXRefAniDBMAL.LoadFromDBCommand(crdb);
                    return cr_WebCacheDeleteXRefAniDBMAL;

                case CommandRequestType.AniDB_GetFileUDP:
                    CommandRequest_GetFile cr_AniDB_GetFileUDP = new CommandRequest_GetFile();
                    cr_AniDB_GetFileUDP.LoadFromDBCommand(crdb);
                    return cr_AniDB_GetFileUDP;

                case CommandRequestType.ReadMediaInfo:
                    CommandRequest_ReadMediaInfo cr_ReadMediaInfo = new CommandRequest_ReadMediaInfo();
                    cr_ReadMediaInfo.LoadFromDBCommand(crdb);
                    return cr_ReadMediaInfo;

                case CommandRequestType.Trakt_UpdateAllSeries:
                    CommandRequest_TraktUpdateAllSeries cr_Trakt_UpdateAllSeries =
                        new CommandRequest_TraktUpdateAllSeries();
                    cr_Trakt_UpdateAllSeries.LoadFromDBCommand(crdb);
                    return cr_Trakt_UpdateAllSeries;

                case CommandRequestType.Trakt_EpisodeCollection:
                    CommandRequest_TraktCollectionEpisode cr_TraktCollectionEpisode =
                        new CommandRequest_TraktCollectionEpisode();
                    cr_TraktCollectionEpisode.LoadFromDBCommand(crdb);
                    return cr_TraktCollectionEpisode;

                case CommandRequestType.Trakt_SyncCollection:
                    CommandRequest_TraktSyncCollection cr_Trakt_SyncCollection =
                        new CommandRequest_TraktSyncCollection();
                    cr_Trakt_SyncCollection.LoadFromDBCommand(crdb);
                    return cr_Trakt_SyncCollection;

                case CommandRequestType.Trakt_EpisodeHistory:
                    CommandRequest_TraktHistoryEpisode cr_Trakt_EpisodeHistory =
                        new CommandRequest_TraktHistoryEpisode();
                    cr_Trakt_EpisodeHistory.LoadFromDBCommand(crdb);
                    return cr_Trakt_EpisodeHistory;

                case CommandRequestType.Trakt_UpdateInfoImages:
                    CommandRequest_TraktUpdateInfoAndImages cr_Trakt_UpdateInfoImages =
                        new CommandRequest_TraktUpdateInfoAndImages();
                    cr_Trakt_UpdateInfoImages.LoadFromDBCommand(crdb);
                    return cr_Trakt_UpdateInfoImages;

                case CommandRequestType.WebCache_SendXRefAniDBTrakt:
                    CommandRequest_WebCacheSendXRefAniDBTrakt cr_WebCache_SendXRefAniDBTrakt =
                        new CommandRequest_WebCacheSendXRefAniDBTrakt();
                    cr_WebCache_SendXRefAniDBTrakt.LoadFromDBCommand(crdb);
                    return cr_WebCache_SendXRefAniDBTrakt;

                case CommandRequestType.WebCache_DeleteXRefAniDBTrakt:
                    CommandRequest_WebCacheDeleteXRefAniDBTrakt cr_WebCache_DeleteXRefAniDBTrakt =
                        new CommandRequest_WebCacheDeleteXRefAniDBTrakt();
                    cr_WebCache_DeleteXRefAniDBTrakt.LoadFromDBCommand(crdb);
                    return cr_WebCache_DeleteXRefAniDBTrakt;

                case CommandRequestType.Trakt_SearchAnime:
                    CommandRequest_TraktSearchAnime cr_Trakt_SearchAnime = new CommandRequest_TraktSearchAnime();
                    cr_Trakt_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_Trakt_SearchAnime;

                case CommandRequestType.MovieDB_SearchAnime:
                    CommandRequest_MovieDBSearchAnime cr_MovieDB_SearchAnime = new CommandRequest_MovieDBSearchAnime();
                    cr_MovieDB_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_MovieDB_SearchAnime;

                case CommandRequestType.WebCache_DeleteXRefAniDBOther:
                    CommandRequest_WebCacheDeleteXRefAniDBOther cr_SendXRefAniDBOther =
                        new CommandRequest_WebCacheDeleteXRefAniDBOther();
                    cr_SendXRefAniDBOther.LoadFromDBCommand(crdb);
                    return cr_SendXRefAniDBOther;

                case CommandRequestType.WebCache_SendXRefAniDBOther:
                    CommandRequest_WebCacheSendXRefAniDBOther cr_WebCacheSendXRefAniDBOther =
                        new CommandRequest_WebCacheSendXRefAniDBOther();
                    cr_WebCacheSendXRefAniDBOther.LoadFromDBCommand(crdb);
                    return cr_WebCacheSendXRefAniDBOther;

                case CommandRequestType.AniDB_DeleteFileUDP:
                    CommandRequest_DeleteFileFromMyList cr_AniDB_DeleteFileUDP =
                        new CommandRequest_DeleteFileFromMyList();
                    cr_AniDB_DeleteFileUDP.LoadFromDBCommand(crdb);
                    return cr_AniDB_DeleteFileUDP;

                case CommandRequestType.ImageDownload:
                    CommandRequest_DownloadImage cr_ImageDownload = new CommandRequest_DownloadImage();
                    cr_ImageDownload.LoadFromDBCommand(crdb);
                    return cr_ImageDownload;

                case CommandRequestType.WebCache_DeleteXRefAniDBTvDB:
                    CommandRequest_WebCacheDeleteXRefAniDBTvDB cr_DeleteXRefAniDBTvDB =
                        new CommandRequest_WebCacheDeleteXRefAniDBTvDB();
                    cr_DeleteXRefAniDBTvDB.LoadFromDBCommand(crdb);
                    return cr_DeleteXRefAniDBTvDB;

                case CommandRequestType.WebCache_SendXRefAniDBTvDB:
                    CommandRequest_WebCacheSendXRefAniDBTvDB cr_SendXRefAniDBTvDB =
                        new CommandRequest_WebCacheSendXRefAniDBTvDB();
                    cr_SendXRefAniDBTvDB.LoadFromDBCommand(crdb);
                    return cr_SendXRefAniDBTvDB;


                case CommandRequestType.TvDB_SearchAnime:
                    CommandRequest_TvDBSearchAnime cr_TvDB_SearchAnime = new CommandRequest_TvDBSearchAnime();
                    cr_TvDB_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_TvDB_SearchAnime;

                case CommandRequestType.TvDB_DownloadImages:
                    CommandRequest_TvDBDownloadImages cr_TvDB_DownloadImages = new CommandRequest_TvDBDownloadImages();
                    cr_TvDB_DownloadImages.LoadFromDBCommand(crdb);
                    return cr_TvDB_DownloadImages;

                case CommandRequestType.TvDB_SeriesEpisodes:
                    CommandRequest_TvDBUpdateSeriesAndEpisodes cr_TvDB_Episodes =
                        new CommandRequest_TvDBUpdateSeriesAndEpisodes();
                    cr_TvDB_Episodes.LoadFromDBCommand(crdb);
                    return cr_TvDB_Episodes;

                case CommandRequestType.AniDB_SyncVotes:
                    CommandRequest_SyncMyVotes cr_SyncVotes = new CommandRequest_SyncMyVotes();
                    cr_SyncVotes.LoadFromDBCommand(crdb);
                    return cr_SyncVotes;

                case CommandRequestType.AniDB_VoteAnime:
                    CommandRequest_VoteAnime cr_VoteAnime = new CommandRequest_VoteAnime();
                    cr_VoteAnime.LoadFromDBCommand(crdb);
                    return cr_VoteAnime;

                case CommandRequestType.AniDB_GetCalendar:
                    CommandRequest_GetCalendar cr_GetCalendar = new CommandRequest_GetCalendar();
                    cr_GetCalendar.LoadFromDBCommand(crdb);
                    return cr_GetCalendar;

                case CommandRequestType.AniDB_GetReleaseGroup:
                    CommandRequest_GetReleaseGroup cr_GetReleaseGroup = new CommandRequest_GetReleaseGroup();
                    cr_GetReleaseGroup.LoadFromDBCommand(crdb);
                    return cr_GetReleaseGroup;

                case CommandRequestType.AniDB_GetAnimeHTTP:
                    CommandRequest_GetAnimeHTTP cr_geth = new CommandRequest_GetAnimeHTTP();
                    cr_geth.LoadFromDBCommand(crdb);
                    return cr_geth;

                case CommandRequestType.AniDB_GetReleaseGroupStatus:
                    CommandRequest_GetReleaseGroupStatus cr_GetReleaseGroupStatus =
                        new CommandRequest_GetReleaseGroupStatus();
                    cr_GetReleaseGroupStatus.LoadFromDBCommand(crdb);
                    return cr_GetReleaseGroupStatus;

                case CommandRequestType.HashFile:
                    CommandRequest_HashFile cr_HashFile = new CommandRequest_HashFile();
                    cr_HashFile.LoadFromDBCommand(crdb);
                    return cr_HashFile;

                case CommandRequestType.ProcessFile:
                    CommandRequest_ProcessFile cr_pf = new CommandRequest_ProcessFile();
                    cr_pf.LoadFromDBCommand(crdb);
                    return cr_pf;

                case CommandRequestType.AniDB_AddFileUDP:
                    CommandRequest_AddFileToMyList cr_af = new CommandRequest_AddFileToMyList();
                    cr_af.LoadFromDBCommand(crdb);
                    return cr_af;

                case CommandRequestType.AniDB_UpdateWatchedUDP:
                    CommandRequest_UpdateMyListFileStatus cr_umlf = new CommandRequest_UpdateMyListFileStatus();
                    cr_umlf.LoadFromDBCommand(crdb);
                    return cr_umlf;

                case CommandRequestType.WebCache_DeleteXRefFileEpisode:
                    CommandRequest_WebCacheDeleteXRefFileEpisode cr_DeleteXRefFileEpisode =
                        new CommandRequest_WebCacheDeleteXRefFileEpisode();
                    cr_DeleteXRefFileEpisode.LoadFromDBCommand(crdb);
                    return cr_DeleteXRefFileEpisode;

                case CommandRequestType.WebCache_SendXRefFileEpisode:
                    CommandRequest_WebCacheSendXRefFileEpisode cr_SendXRefFileEpisode =
                        new CommandRequest_WebCacheSendXRefFileEpisode();
                    cr_SendXRefFileEpisode.LoadFromDBCommand(crdb);
                    return cr_SendXRefFileEpisode;

                case CommandRequestType.AniDB_GetReviews:
                    CommandRequest_GetReviews cr_GetReviews = new CommandRequest_GetReviews();
                    cr_GetReviews.LoadFromDBCommand(crdb);
                    return cr_GetReviews;

                case CommandRequestType.AniDB_GetUpdated:
                    CommandRequest_GetUpdated cr_GetUpdated = new CommandRequest_GetUpdated();
                    cr_GetUpdated.LoadFromDBCommand(crdb);
                    return cr_GetUpdated;

                case CommandRequestType.AniDB_SyncMyList:
                    CommandRequest_SyncMyList cr_SyncMyList = new CommandRequest_SyncMyList();
                    cr_SyncMyList.LoadFromDBCommand(crdb);
                    return cr_SyncMyList;

                case CommandRequestType.Refresh_AnimeStats:
                    CommandRequest_RefreshAnime cr_refreshAnime = new CommandRequest_RefreshAnime();
                    cr_refreshAnime.LoadFromDBCommand(crdb);
                    return cr_refreshAnime;

                case CommandRequestType.LinkAniDBTvDB:
                    CommandRequest_LinkAniDBTvDB cr_linkAniDBTvDB = new CommandRequest_LinkAniDBTvDB();
                    cr_linkAniDBTvDB.LoadFromDBCommand(crdb);
                    return cr_linkAniDBTvDB;

                case CommandRequestType.Refresh_GroupFilter:
                    CommandRequest_RefreshGroupFilter cr_refreshGroupFilter = new CommandRequest_RefreshGroupFilter();
                    cr_refreshGroupFilter.LoadFromDBCommand(crdb);
                    return cr_refreshGroupFilter;

                case CommandRequestType.Plex_Sync:
                    CommandRequest_PlexSyncWatched cr_PlexSync = new CommandRequest_PlexSyncWatched();
                    cr_PlexSync.LoadFromDBCommand(crdb);
                    return cr_PlexSync;
                case CommandRequestType.LinkFileManually:
                    CommandRequest_LinkFileManually cr_LinkFile = new CommandRequest_LinkFileManually();
                    cr_LinkFile.LoadFromDBCommand(crdb);
                    return cr_LinkFile;
            }

            return null;
        }
    }
}