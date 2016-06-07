using JMMServer.Commands.AniDB;
using JMMServer.Commands.Azure;
using JMMServer.Commands.MAL;
using JMMServer.Commands.WebCache;
using JMMServer.Entities;

namespace JMMServer.Commands
{
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
            var crt = (CommandRequestType)crdb.CommandType;
            switch (crt)
            {
                case CommandRequestType.Trakt_SyncCollectionSeries:
                    var cr_CommandRequest_TraktSyncCollectionSeries = new CommandRequest_TraktSyncCollectionSeries();
                    cr_CommandRequest_TraktSyncCollectionSeries.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_TraktSyncCollectionSeries;

                case CommandRequestType.AniDB_GetEpisodeUDP:
                    var cr_CommandRequest_GetEpisode = new CommandRequest_GetEpisode();
                    cr_CommandRequest_GetEpisode.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_GetEpisode;

                case CommandRequestType.Azure_SendAnimeTitle:
                    var cr_CommandRequest_Azure_SendAnimeTitle = new CommandRequest_Azure_SendAnimeTitle();
                    cr_CommandRequest_Azure_SendAnimeTitle.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendAnimeTitle;

                case CommandRequestType.AniDB_GetTitles:
                    var cr_CommandRequest_GetAniDBTitles = new CommandRequest_GetAniDBTitles();
                    cr_CommandRequest_GetAniDBTitles.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_GetAniDBTitles;

                case CommandRequestType.Azure_SendAnimeXML:
                    var cr_CommandRequest_Azure_SendAnimeXML = new CommandRequest_Azure_SendAnimeXML();
                    cr_CommandRequest_Azure_SendAnimeXML.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendAnimeXML;

                case CommandRequestType.Azure_SendAnimeFull:
                    var cr_CommandRequest_Azure_SendAnimeFull = new CommandRequest_Azure_SendAnimeFull();
                    cr_CommandRequest_Azure_SendAnimeFull.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendAnimeFull;

                case CommandRequestType.Azure_SendUserInfo:
                    var cr_CommandRequest_Azure_SendUserInfo = new CommandRequest_Azure_SendUserInfo();
                    cr_CommandRequest_Azure_SendUserInfo.LoadFromDBCommand(crdb);
                    return cr_CommandRequest_Azure_SendUserInfo;

                case CommandRequestType.AniDB_UpdateMylistStats:
                    var cr_AniDB_UpdateMylistStats = new CommandRequest_UpdateMylistStats();
                    cr_AniDB_UpdateMylistStats.LoadFromDBCommand(crdb);
                    return cr_AniDB_UpdateMylistStats;

                case CommandRequestType.MAL_DownloadWatchedStates:
                    var cr_MAL_DownloadWatchedStates = new CommandRequest_MALDownloadStatusFromMAL();
                    cr_MAL_DownloadWatchedStates.LoadFromDBCommand(crdb);
                    return cr_MAL_DownloadWatchedStates;

                case CommandRequestType.MAL_UploadWatchedStates:
                    var cr_MAL_UploadWatchedStates = new CommandRequest_MALUploadStatusToMAL();
                    cr_MAL_UploadWatchedStates.LoadFromDBCommand(crdb);
                    return cr_MAL_UploadWatchedStates;

                case CommandRequestType.MAL_UpdateStatus:
                    var cr_MAL_UpdateStatus = new CommandRequest_MALUpdatedWatchedStatus();
                    cr_MAL_UpdateStatus.LoadFromDBCommand(crdb);
                    return cr_MAL_UpdateStatus;

                case CommandRequestType.MAL_SearchAnime:
                    var cr_MAL_SearchAnime = new CommandRequest_MALSearchAnime();
                    cr_MAL_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_MAL_SearchAnime;

                case CommandRequestType.WebCache_SendXRefAniDBMAL:
                    var cr_WebCacheSendXRefAniDBMAL = new CommandRequest_WebCacheSendXRefAniDBMAL();
                    cr_WebCacheSendXRefAniDBMAL.LoadFromDBCommand(crdb);
                    return cr_WebCacheSendXRefAniDBMAL;

                case CommandRequestType.WebCache_DeleteXRefAniDBMAL:
                    var cr_WebCacheDeleteXRefAniDBMAL = new CommandRequest_WebCacheDeleteXRefAniDBMAL();
                    cr_WebCacheDeleteXRefAniDBMAL.LoadFromDBCommand(crdb);
                    return cr_WebCacheDeleteXRefAniDBMAL;

                case CommandRequestType.AniDB_GetFileUDP:
                    var cr_AniDB_GetFileUDP = new CommandRequest_GetFile();
                    cr_AniDB_GetFileUDP.LoadFromDBCommand(crdb);
                    return cr_AniDB_GetFileUDP;

                case CommandRequestType.ReadMediaInfo:
                    var cr_ReadMediaInfo = new CommandRequest_ReadMediaInfo();
                    cr_ReadMediaInfo.LoadFromDBCommand(crdb);
                    return cr_ReadMediaInfo;

                case CommandRequestType.Trakt_UpdateAllSeries:
                    var cr_Trakt_UpdateAllSeries = new CommandRequest_TraktUpdateAllSeries();
                    cr_Trakt_UpdateAllSeries.LoadFromDBCommand(crdb);
                    return cr_Trakt_UpdateAllSeries;

                case CommandRequestType.Trakt_EpisodeCollection:
                    var cr_TraktCollectionEpisode = new CommandRequest_TraktCollectionEpisode();
                    cr_TraktCollectionEpisode.LoadFromDBCommand(crdb);
                    return cr_TraktCollectionEpisode;

                case CommandRequestType.Trakt_SyncCollection:
                    var cr_Trakt_SyncCollection = new CommandRequest_TraktSyncCollection();
                    cr_Trakt_SyncCollection.LoadFromDBCommand(crdb);
                    return cr_Trakt_SyncCollection;

                case CommandRequestType.Trakt_EpisodeHistory:
                    var cr_Trakt_EpisodeHistory = new CommandRequest_TraktHistoryEpisode();
                    cr_Trakt_EpisodeHistory.LoadFromDBCommand(crdb);
                    return cr_Trakt_EpisodeHistory;

                case CommandRequestType.Trakt_UpdateInfoImages:
                    var cr_Trakt_UpdateInfoImages = new CommandRequest_TraktUpdateInfoAndImages();
                    cr_Trakt_UpdateInfoImages.LoadFromDBCommand(crdb);
                    return cr_Trakt_UpdateInfoImages;

                case CommandRequestType.WebCache_SendXRefAniDBTrakt:
                    var cr_WebCache_SendXRefAniDBTrakt = new CommandRequest_WebCacheSendXRefAniDBTrakt();
                    cr_WebCache_SendXRefAniDBTrakt.LoadFromDBCommand(crdb);
                    return cr_WebCache_SendXRefAniDBTrakt;

                case CommandRequestType.WebCache_DeleteXRefAniDBTrakt:
                    var cr_WebCache_DeleteXRefAniDBTrakt = new CommandRequest_WebCacheDeleteXRefAniDBTrakt();
                    cr_WebCache_DeleteXRefAniDBTrakt.LoadFromDBCommand(crdb);
                    return cr_WebCache_DeleteXRefAniDBTrakt;

                case CommandRequestType.Trakt_SearchAnime:
                    var cr_Trakt_SearchAnime = new CommandRequest_TraktSearchAnime();
                    cr_Trakt_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_Trakt_SearchAnime;

                case CommandRequestType.MovieDB_SearchAnime:
                    var cr_MovieDB_SearchAnime = new CommandRequest_MovieDBSearchAnime();
                    cr_MovieDB_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_MovieDB_SearchAnime;

                case CommandRequestType.WebCache_DeleteXRefAniDBOther:
                    var cr_SendXRefAniDBOther = new CommandRequest_WebCacheDeleteXRefAniDBOther();
                    cr_SendXRefAniDBOther.LoadFromDBCommand(crdb);
                    return cr_SendXRefAniDBOther;

                case CommandRequestType.WebCache_SendXRefAniDBOther:
                    var cr_WebCacheSendXRefAniDBOther = new CommandRequest_WebCacheSendXRefAniDBOther();
                    cr_WebCacheSendXRefAniDBOther.LoadFromDBCommand(crdb);
                    return cr_WebCacheSendXRefAniDBOther;

                case CommandRequestType.AniDB_DeleteFileUDP:
                    var cr_AniDB_DeleteFileUDP = new CommandRequest_DeleteFileFromMyList();
                    cr_AniDB_DeleteFileUDP.LoadFromDBCommand(crdb);
                    return cr_AniDB_DeleteFileUDP;

                case CommandRequestType.ImageDownload:
                    var cr_ImageDownload = new CommandRequest_DownloadImage();
                    cr_ImageDownload.LoadFromDBCommand(crdb);
                    return cr_ImageDownload;

                case CommandRequestType.WebCache_DeleteXRefAniDBTvDB:
                    var cr_DeleteXRefAniDBTvDB = new CommandRequest_WebCacheDeleteXRefAniDBTvDB();
                    cr_DeleteXRefAniDBTvDB.LoadFromDBCommand(crdb);
                    return cr_DeleteXRefAniDBTvDB;

                case CommandRequestType.WebCache_SendXRefAniDBTvDB:
                    var cr_SendXRefAniDBTvDB = new CommandRequest_WebCacheSendXRefAniDBTvDB();
                    cr_SendXRefAniDBTvDB.LoadFromDBCommand(crdb);
                    return cr_SendXRefAniDBTvDB;


                case CommandRequestType.TvDB_SearchAnime:
                    var cr_TvDB_SearchAnime = new CommandRequest_TvDBSearchAnime();
                    cr_TvDB_SearchAnime.LoadFromDBCommand(crdb);
                    return cr_TvDB_SearchAnime;

                case CommandRequestType.TvDB_DownloadImages:
                    var cr_TvDB_DownloadImages = new CommandRequest_TvDBDownloadImages();
                    cr_TvDB_DownloadImages.LoadFromDBCommand(crdb);
                    return cr_TvDB_DownloadImages;

                case CommandRequestType.TvDB_SeriesEpisodes:
                    var cr_TvDB_Episodes = new CommandRequest_TvDBUpdateSeriesAndEpisodes();
                    cr_TvDB_Episodes.LoadFromDBCommand(crdb);
                    return cr_TvDB_Episodes;

                case CommandRequestType.AniDB_SyncVotes:
                    var cr_SyncVotes = new CommandRequest_SyncMyVotes();
                    cr_SyncVotes.LoadFromDBCommand(crdb);
                    return cr_SyncVotes;

                case CommandRequestType.AniDB_VoteAnime:
                    var cr_VoteAnime = new CommandRequest_VoteAnime();
                    cr_VoteAnime.LoadFromDBCommand(crdb);
                    return cr_VoteAnime;

                case CommandRequestType.AniDB_GetCalendar:
                    var cr_GetCalendar = new CommandRequest_GetCalendar();
                    cr_GetCalendar.LoadFromDBCommand(crdb);
                    return cr_GetCalendar;

                case CommandRequestType.AniDB_GetReleaseGroup:
                    var cr_GetReleaseGroup = new CommandRequest_GetReleaseGroup();
                    cr_GetReleaseGroup.LoadFromDBCommand(crdb);
                    return cr_GetReleaseGroup;

                case CommandRequestType.AniDB_GetAnimeHTTP:
                    var cr_geth = new CommandRequest_GetAnimeHTTP();
                    cr_geth.LoadFromDBCommand(crdb);
                    return cr_geth;

                case CommandRequestType.AniDB_GetReleaseGroupStatus:
                    var cr_GetReleaseGroupStatus = new CommandRequest_GetReleaseGroupStatus();
                    cr_GetReleaseGroupStatus.LoadFromDBCommand(crdb);
                    return cr_GetReleaseGroupStatus;

                case CommandRequestType.HashFile:
                    var cr_HashFile = new CommandRequest_HashFile();
                    cr_HashFile.LoadFromDBCommand(crdb);
                    return cr_HashFile;

                case CommandRequestType.ProcessFile:
                    var cr_pf = new CommandRequest_ProcessFile();
                    cr_pf.LoadFromDBCommand(crdb);
                    return cr_pf;

                case CommandRequestType.AniDB_AddFileUDP:
                    var cr_af = new CommandRequest_AddFileToMyList();
                    cr_af.LoadFromDBCommand(crdb);
                    return cr_af;

                case CommandRequestType.AniDB_UpdateWatchedUDP:
                    var cr_umlf = new CommandRequest_UpdateMyListFileStatus();
                    cr_umlf.LoadFromDBCommand(crdb);
                    return cr_umlf;

                case CommandRequestType.WebCache_DeleteXRefFileEpisode:
                    var cr_DeleteXRefFileEpisode = new CommandRequest_WebCacheDeleteXRefFileEpisode();
                    cr_DeleteXRefFileEpisode.LoadFromDBCommand(crdb);
                    return cr_DeleteXRefFileEpisode;

                case CommandRequestType.WebCache_SendXRefFileEpisode:
                    var cr_SendXRefFileEpisode = new CommandRequest_WebCacheSendXRefFileEpisode();
                    cr_SendXRefFileEpisode.LoadFromDBCommand(crdb);
                    return cr_SendXRefFileEpisode;

                case CommandRequestType.AniDB_GetReviews:
                    var cr_GetReviews = new CommandRequest_GetReviews();
                    cr_GetReviews.LoadFromDBCommand(crdb);
                    return cr_GetReviews;

                case CommandRequestType.AniDB_GetUpdated:
                    var cr_GetUpdated = new CommandRequest_GetUpdated();
                    cr_GetUpdated.LoadFromDBCommand(crdb);
                    return cr_GetUpdated;

                case CommandRequestType.AniDB_SyncMyList:
                    var cr_SyncMyList = new CommandRequest_SyncMyList();
                    cr_SyncMyList.LoadFromDBCommand(crdb);
                    return cr_SyncMyList;

                case CommandRequestType.Refresh_AnimeStats:
                    var cr_refreshAnime = new CommandRequest_RefreshAnime();
                    cr_refreshAnime.LoadFromDBCommand(crdb);
                    return cr_refreshAnime;
            }

            return null;
        }
    }
}