using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.Generic
{
    public static class CommandHelper
    {
        private static Dictionary<CommandRequestType, Type> _commandRequestImpls;

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

        public static void LoadCommands(IServiceProvider provider)
        {
            var logFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = logFactory.CreateLogger(typeof(CommandHelper));
            _commandRequestImpls = new Dictionary<CommandRequestType, Type>();

            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[]{}; }}).Where(a => a.GetCustomAttribute<CommandAttribute>() != null);
            foreach (var commandType in types)
            {
                var attr = commandType.GetCustomAttribute<CommandAttribute>()!.RequestType;
                if (_commandRequestImpls.ContainsKey(attr))
                {
                    logger.LogWarning("Duplicate command of type {Attr}, this should never happen without a code error, please report this to the devs", attr);
                    continue;
                }

                _commandRequestImpls.Add(attr, commandType);
            }
        }


        public static ICommandRequest GetCommand(IServiceProvider provider, CommandRequest crdb)
        {
            var logFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = logFactory.CreateLogger(typeof(CommandHelper));
            if (crdb == null)
                return null;

            if (!_commandRequestImpls.TryGetValue((CommandRequestType)crdb.CommandType, out var type)) return null;

            try
            {
                var command = ActivatorUtilities.CreateInstance(provider, type) as ICommandRequest;
                command?.LoadFromDBCommand(crdb);
                return command;
            }
            catch (Exception e)
            {
                logger.LogError("There was an error loading {CommandType}: The XML was {CommandDetails}", (CommandRequestType)crdb.CommandType, crdb.CommandDetails);
                logger.LogError("There was an error loading {CommandType}: {E}", (CommandRequestType)crdb.CommandType, e);
            }
            return null;
        }
    }
}