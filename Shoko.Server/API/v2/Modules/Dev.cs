using System.Collections.Generic;
using System.Text;
using Nancy;
using Nancy.Responses;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v2.Modules
{
    public class Dev : Nancy.NancyModule
    {
        public Dev() : base("/api/dev")
        {
#if DEBUG
            Get["/contracts/{entity?}"] = x => ExtractContracts((string) x.entity);
#endif
            Get["/commands"] = x => GetCommands();
        }

        private object GetCommands()
        {
            Dictionary<int, string> commands = new Dictionary<int,string>();
            commands[(int) CommandRequestType.AniDB_AddFileUDP] = "CommandRequest_AddFileToMyList";
            commands[(int) CommandRequestType.AniDB_DeleteFileUDP] = "CommandRequest_DeleteFileFromMyList";
            commands[(int) CommandRequestType.AniDB_GetAnimeHTTP] = "CommandRequest_GetAnimeHTTP";
            commands[(int) CommandRequestType.AniDB_GetCalendar] = "CommandRequest_GetCalendar";
            commands[(int) CommandRequestType.AniDB_GetEpisodeUDP] = "CommandRequest_GetEpisode";
            commands[(int) CommandRequestType.AniDB_GetFileUDP] = "CommandRequest_GetFile";
            commands[(int) CommandRequestType.AniDB_GetMyListFile] = "CommandRequest_GetFileMyListStatus";
            commands[(int) CommandRequestType.AniDB_GetReleaseGroup] = "CommandRequest_GetReleaseGroup";
            commands[(int) CommandRequestType.AniDB_GetReleaseGroupStatus] = "CommandRequest_GetReleaseGroupStatus";
            commands[(int) CommandRequestType.AniDB_GetReviews] = "CommandRequest_GetReviews";
            commands[(int) CommandRequestType.AniDB_GetTitles] = "CommandRequest_GetAniDBTitles";
            commands[(int) CommandRequestType.AniDB_GetUpdated] = "CommandRequest_GetUpdated";
            commands[(int) CommandRequestType.AniDB_SyncMyList] = "CommandRequest_SyncMyList";
            commands[(int) CommandRequestType.AniDB_SyncVotes] = "CommandRequest_SyncMyVotes";
            commands[(int) CommandRequestType.AniDB_UpdateMylistStats] = "CommandRequest_UpdateMyListStats";
            commands[(int) CommandRequestType.AniDB_UpdateWatchedUDP] = "CommandRequest_UpdateMyListFileStatus";
            commands[(int) CommandRequestType.AniDB_VoteAnime] = "CommandRequest_VoteAnime";
            commands[(int) CommandRequestType.Azure_SendAnimeFull] = "CommandRequest_Azure_SendAnimeFull";
            commands[(int) CommandRequestType.Azure_SendAnimeTitle] = "CommandRequest_Azure_SendAnimeTitle";
            commands[(int) CommandRequestType.Azure_SendAnimeXML] = "CommandRequest_Azure_SendAnimeXML";
            commands[(int) CommandRequestType.Azure_SendUserInfo] = "CommandRequest_Azure_SendUserInfo";
            commands[(int) CommandRequestType.HashFile] = "CommandRequest_HashFile";
            commands[(int) CommandRequestType.ImageDownload] = "CommandRequest_DownloadImage";
            commands[(int) CommandRequestType.LinkAniDBTvDB] = "CommandRequest_LinkAniDBTvDB";
            commands[(int) CommandRequestType.LinkFileManually] = "CommandRequest_LinkFileManually";
            commands[(int) CommandRequestType.MAL_DownloadWatchedStates] = "CommandRequest_MALDownloadStatusFromMAL";
            commands[(int) CommandRequestType.MAL_SearchAnime] = "CommandRequest_MALSearchAnime";
            commands[(int) CommandRequestType.MAL_UpdateStatus] = "CommandRequest_MALUpdatedWatchedStatus";
            commands[(int) CommandRequestType.MAL_UploadWatchedStates] = "CommandRequest_MALUploadStatusToMAL";
            commands[(int) CommandRequestType.MovieDB_SearchAnime] = "CommandRequest_MovieDBSearchAnime";
            commands[(int) CommandRequestType.Plex_Sync] = "CommandRequest_PlexSyncWatched";
            commands[(int) CommandRequestType.ProcessFile] = "CommandRequest_ProcessFile";
            commands[(int) CommandRequestType.ReadMediaInfo] = "CommandRequest_ReadMediaInfo";
            commands[(int) CommandRequestType.Refresh_AnimeStats] = "CommandRequest_RefreshAnime";
            commands[(int) CommandRequestType.Refresh_GroupFilter] = "CommandRequest_RefreshGroupFilter";
            commands[(int) CommandRequestType.Trakt_EpisodeCollection] = "CommandRequest_TraktCollectionEpisode";
            commands[(int) CommandRequestType.Trakt_EpisodeHistory] = "CommandRequest_TraktHistoryEpisode";
            commands[(int) CommandRequestType.Trakt_SearchAnime] = "CommandRequest_TraktSearchAnime";
            commands[(int) CommandRequestType.Trakt_SyncCollection] = "CommandRequest_TraktSyncCollection";
            commands[(int) CommandRequestType.Trakt_SyncCollectionSeries] = "CommandRequest_TraktSyncCollectionSeries";
            commands[(int) CommandRequestType.Trakt_UpdateAllSeries] = "CommandRequest_TraktUpdateAllSeries";
            commands[(int) CommandRequestType.Trakt_UpdateInfo] = "CommandRequest_TraktUpdateInfo";
            commands[(int) CommandRequestType.TvDB_DownloadImages] = "CommandRequest_TvDBDownloadImages";
            commands[(int) CommandRequestType.TvDB_SearchAnime] = "CommandRequest_TvDBSearchAnime";
            commands[(int) CommandRequestType.TvDB_UpdateEpisode] = "CommandRequest_TvDBUpdateEpisode";
            commands[(int) CommandRequestType.TvDB_UpdateSeries] = "CommandRequest_TvDBUpdateSeries";
            commands[(int) CommandRequestType.ValidateAllImages] = "CommandRequest_ValidateAllImages";
            commands[(int) CommandRequestType.WebCache_DeleteXRefAniDBMAL] = "CommandRequest_WebCacheDeleteXRefAniDBMAL";
            commands[(int) CommandRequestType.WebCache_DeleteXRefAniDBOther] = "CommandRequest_WebCacheDeleteXRefAniDBOther";
            commands[(int) CommandRequestType.WebCache_DeleteXRefAniDBTrakt] = "CommandRequest_WebCacheDeleteXRefAniDBTrakt";
            commands[(int) CommandRequestType.WebCache_DeleteXRefAniDBTvDB] = "CommandRequest_WebCacheDeleteXRefAniDBTvDB";
            commands[(int) CommandRequestType.WebCache_DeleteXRefFileEpisode] = "CommandRequest_WebCacheDeleteXRefFileEpisode";
            commands[(int) CommandRequestType.WebCache_SendXRefAniDBMAL] = "CommandRequest_WebCacheSendXRefAniDBMAL";
            commands[(int) CommandRequestType.WebCache_SendXRefAniDBOther] = "CommandRequest_WebCacheSendXRefAniDBOther";
            commands[(int) CommandRequestType.WebCache_SendXRefAniDBTrakt] = "CommandRequest_WebCacheSendXRefAniDBTrakt";
            commands[(int) CommandRequestType.WebCache_SendXRefAniDBTvDB] = "CommandRequest_WebCacheSendXRefAniDBTvDB";
            commands[(int) CommandRequestType.WebCache_SendXRefFileEpisode] = "CommandRequest_WebCacheSendXRefFileEpisode";


            StringBuilder str = new StringBuilder();
            foreach (int commandType in commands.Keys)
            {
                string commandclassstring =
                    $"    public class {commands[commandType]}Map : SubclassMap<{commands[commandType]}>\n    {{\n        public {commands[commandType]}Map()\n        {{\n            DiscriminatorValue((int) CommandRequestType.{(CommandRequestType) commandType});\n        }}\n    }}";
                str.AppendLine(commandclassstring);
                str.AppendLine(string.Empty);
            }
            return str.ToString();
        }

        /// <summary>
        /// Dumps the contracts as JSON files embedded in a zip file.
        /// </summary>
        /// <param name="entityType">The type of the entity to dump (can be <see cref="string.Empty"/> or <c>null</c> to dump all).</param>
        private object ExtractContracts(string entityType)
        {
            var zipStream = new ContractExtractor().GetContractsAsZipStream(entityType);

            return new StreamResponse(() => zipStream, "application/zip").AsAttachment("contracts.zip");
        }
    }
}