using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands
{
    public static class CommandHelper
    {
        private static Dictionary<CommandRequestType, ReflectionUtils.ObjectActivator<CommandRequestImplementation>> CommandRequestImpls;
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
        // - This is here to force it to run before the next GetAnimeHTTP, so that we don't need to wait for images
        // CommandRequest_DownloadAniDBImages
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

        public static void LoadCommands()
        {
            CommandRequestImpls = new Dictionary<CommandRequestType, ReflectionUtils.ObjectActivator<CommandRequestImplementation>>();

            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[]{}; }}).Where(a => a.GetCustomAttribute<CommandAttribute>() != null);
            foreach (var commandType in types)
            {
                var attr = commandType.GetCustomAttribute<CommandAttribute>().RequestType;
                if (CommandRequestImpls.ContainsKey(attr))
                {
                    logger.Warn($"Duplicate command of type {attr}, this should never happen without a code error, please report this to the devs.");
                    continue;
                }

                var ctor = ReflectionUtils.GetActivator<CommandRequestImplementation>(commandType.GetConstructor(Type.EmptyTypes));
                CommandRequestImpls.Add(attr, ctor);
            }
        }


        public static ICommandRequest GetCommand(CommandRequest crdb)
        {
            if (crdb == null)
                return null;

            if (!CommandRequestImpls.TryGetValue((CommandRequestType)crdb.CommandType,
                out ReflectionUtils.ObjectActivator<CommandRequestImplementation> ctor)) return null;

            try
            {
                CommandRequestImplementation command = ctor();
                command.LoadFromDBCommand(crdb);
                return command;
            }
            catch (Exception e)
            {
                logger.Error($"There was an error loading {(CommandRequestType)crdb.CommandType}: The XML was {crdb.CommandDetails}");
                logger.Error($"There was an error loading {(CommandRequestType)crdb.CommandType}: {e}");
            }
            return null;
        }
    }
}