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
            if (req.CommandType == (int) CommandRequestType.TvDB_DownloadImages ||
                req.CommandType == (int) CommandRequestType.ImageDownload ||
                req.CommandType == (int) CommandRequestType.ValidateAllImages)
                return 2;
            if (req.CommandType == (int) CommandRequestType.HashFile ||
                     req.CommandType == (int) CommandRequestType.ReadMediaInfo)
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
                using (RepoLock.ReaderLock())
                {
                    if (IsCached)
                        return CommandTypes.GetMultiple(0).OrderBy(a => a.Priority).ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
                    return Table.Where(a => a.CommandType==0).OrderBy(a => a.Priority).ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
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
                return Table.Where(a => a.CommandType == 0).ToList();
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
                    return Table.Where(a => a.CommandType == 1).ToList().OrderBy(a => a.Priority)
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
                return Table.Where(a => a.CommandType == 1).ToList();
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
                    return Table.Where(a => a.CommandType == 2).ToList().OrderBy(a => a.Priority)
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
                return Table.Where(a => a.CommandType == 2).ToList();
            }
        }

        public int GetQueuedCommandCountGeneral()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(0).Count;
                return Table.Count(a => a.CommandType == 0);
            }
        }

        public int GetQueuedCommandCountHasher()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(1).Count;
                return Table.Count(a => a.CommandType == 1);
            }
        }

        public int GetQueuedCommandCountImages()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CommandTypes.GetMultiple(2).Count;
                return Table.Count(a => a.CommandType == 2);
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