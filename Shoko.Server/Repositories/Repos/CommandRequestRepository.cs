using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class CommandRequestRepository : BaseRepository<CommandRequest, int>
    {
        private PocoIndex<int, CommandRequest, string> CommandIDs;
        private PocoIndex<int, CommandRequest, int> CommandTypes;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        //TODO: Refactor to attributes.
        private static readonly HashSet<int> CommandTypesHasher = new HashSet<int>
        {
            (int) CommandRequestType.HashFile,
            (int) CommandRequestType.ReadMediaInfo
        };

        private static readonly HashSet<int> CommandTypesImages = new HashSet<int>
        {
            (int) CommandRequestType.TvDB_DownloadImages,
            (int) CommandRequestType.ImageDownload,
            (int) CommandRequestType.ValidateAllImages
        };

        private static readonly HashSet<int> AniDbUdpCommands = new HashSet<int>
        {
            (int) CommandRequestType.AniDB_AddFileUDP,
            (int) CommandRequestType.AniDB_DeleteFileUDP,
            (int) CommandRequestType.AniDB_GetCalendar,
            (int) CommandRequestType.AniDB_GetEpisodeUDP,
            (int) CommandRequestType.AniDB_GetFileUDP,
            (int) CommandRequestType.AniDB_GetMyListFile,
            (int) CommandRequestType.AniDB_GetReleaseGroup,
            (int) CommandRequestType.AniDB_GetReleaseGroupStatus,
            (int) CommandRequestType.AniDB_GetReviews, // this isn't used.
            (int) CommandRequestType.AniDB_GetUpdated,
            (int) CommandRequestType.AniDB_UpdateWatchedUDP,
            (int) CommandRequestType.AniDB_UpdateMylistStats,
            (int) CommandRequestType.AniDB_VoteAnime
        };

        private static readonly HashSet<int> AniDbHttpCommands = new HashSet<int>
        {
            (int) CommandRequestType.AniDB_GetAnimeHTTP,
            (int) CommandRequestType.AniDB_SyncMyList,
            (int) CommandRequestType.AniDB_SyncVotes,
        };

        private static readonly HashSet<int> CommandTypesGeneral = Enum.GetValues(typeof(CommandRequestType))
            .OfType<CommandRequestType>().Select(a => (int)a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .ToHashSet();

        private static readonly HashSet<int> CommandTypesGeneralUDPBan = Enum.GetValues(typeof(CommandRequestType))
            .OfType<CommandRequestType>().Select(a => (int)a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .Except(AniDbUdpCommands).ToHashSet();

        private static readonly HashSet<int> CommandTypesGeneralHTTPBan = Enum.GetValues(typeof(CommandRequestType))
            .OfType<CommandRequestType>().Select(a => (int)a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .Except(AniDbHttpCommands).ToHashSet();

        private static readonly HashSet<int> CommandTypesGeneralFullBan = Enum.GetValues(typeof(CommandRequestType))
            .OfType<CommandRequestType>().Select(a => (int)a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .Except(AniDbUdpCommands).Except(AniDbHttpCommands).ToHashSet();

        internal override int SelectKey(CommandRequest entity) => entity.CommandRequestID;


        internal override void PopulateIndexes()
        {
            CommandIDs = new PocoIndex<int, CommandRequest, string>(Cache, a => a.CommandID);
            CommandTypes = new PocoIndex<int, CommandRequest, int>(Cache, GetQueueIndex);
        }

        internal override void ClearIndexes()
        {
            CommandIDs = null;
            CommandTypes = null;
        }

        /// <summary>
        /// Returns a numeric index for which queue to use
        /// </summary>
        /// <param name="req"></param>
        /// <returns>
        /// 0 = General
        /// 1 = Hasher
        /// 2 = Images
        /// </returns>
        public static int GetQueueIndex(CommandRequest req)
        {
            if (CommandTypesImages.Contains(req.CommandType))
                return 2;
            if (CommandTypesHasher.Contains(req.CommandType))
                return 1;

            return 0;
        }



        public CommandRequest GetByCommandID(string cmdid)
        {
            if (string.IsNullOrEmpty(cmdid)) return null;
            List<CommandRequest> cmds;
            using (RepoLock.ReaderLock())
            {
                cmds = IsCached
                    ? CommandIDs.GetMultiple(cmdid)
                    : Table.Where(a => a.CommandID == cmdid).ToList();
            }

            CommandRequest cmd = cmds.FirstOrDefault();
            if (cmds.Count <= 1) return cmd;
            cmds.Remove(cmd);
            Delete(cmds);
            return cmd;
        }


        public CommandRequest GetNextDBCommandRequestGeneral()
        {
            try
            {
                HashSet<int> types = CommandTypesGeneral;
                // This is called very often, so speed it up as much as possible
                // We can spare bytes of RAM to speed up the command queue
                if (ShokoService.AnidbProcessor.IsHttpBanned && ShokoService.AnidbProcessor.IsUdpBanned)
                {
                    types = CommandTypesGeneralFullBan;
                }
                else if (ShokoService.AnidbProcessor.IsUdpBanned)
                {
                    types = CommandTypesGeneralUDPBan;
                }
                else if (ShokoService.AnidbProcessor.IsHttpBanned)
                {
                    types = CommandTypesGeneralHTTPBan;
                }

                using (RepoLock.ReaderLock())
                {
                    if (IsCached)
                        return GetAll().Where(a => types.Contains(a.CommandType)).OrderBy(a => a.Priority).ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
                    return Table.Where(a => types.Contains(a.CommandType)).OrderBy(a => a.Priority).ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
                }

            }
            catch (Exception e)
            {
                logger.Error($"There was an error retrieving the next command for the General Queue: {e}");
                return null;
            }
        }

        public List<CommandRequest> GetAllCommandRequestGeneral()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(0);
                return Table.Where(a => GetQueueIndex(a) == 0).ToList();
            }

        }

        public CommandRequest GetNextDBCommandRequestHasher()
        {
            try
            {
                using (RepoLock.ReaderLock())
                {
                    if (IsCached)
                        return CommandTypes.GetMultiple(1).OrderBy(a => a.Priority)
                            .ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
                    return Table.Where(a => GetQueueIndex(a) == 1).ToList().OrderBy(a => a.Priority)
                        .ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                logger.Error($"There was an error retrieving the next command for the Hasher Queue: {e}");
                return null;
            }
        }

        public List<CommandRequest> GetAllCommandRequestHasher()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(1);
                return Table.Where(a => GetQueueIndex(a) == 1).ToList();
            }
        }

        public CommandRequest GetNextDBCommandRequestImages()
        {
            try
            {
                using (RepoLock.ReaderLock())
                {
                    if (IsCached)
                        return CommandTypes.GetMultiple(2).OrderBy(a => a.Priority)
                            .ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
                    return Table.Where(a => GetQueueIndex(a) == 2).ToList().OrderBy(a => a.Priority)
                        .ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                logger.Error($"There was an error retrieving the next command for the Image Queue: {e}");
                return null;
            }
        }

        public List<CommandRequest> GetAllCommandRequestImages()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(2);
                return Table.Where(a => GetQueueIndex(a) == 2).ToList();
            }
        }

        public int GetQueuedCommandCountGeneral()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(0).Count;
                return Table.Count(a => GetQueueIndex(a) == 0);
            }
        }

        public int GetQueuedCommandCountHasher()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(1).Count;
                return Table.Count(a => GetQueueIndex(a) == 1);
            }
        }

        public int GetQueuedCommandCountImages()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(2).Count;
                return Table.Count(a => GetQueueIndex(a) == 2);
            }

        }

        public void ClearGeneralQueue()
        {
            using (RepoLock.ReaderLock())
            {
                GetAllCommandRequestGeneral().ForEach(s => Delete(s));
            }
        }

        public void ClearHasherQueue()
        {
            using (RepoLock.ReaderLock())
            {
                GetAllCommandRequestHasher().ForEach(s => Delete(s));
            }
        }

        public void ClearImageQueue()
        {
            using (RepoLock.ReaderLock())
            {
                GetAllCommandRequestImages().ForEach(s => Delete(s));
            }
        }
    }
}