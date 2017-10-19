using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class CommandRequestRepository : BaseCachedRepository<CommandRequest, int>
    {
        private PocoIndex<int, CommandRequest, string> CommandIDs;
        private PocoIndex<int, CommandRequest, int> CommandTypes;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private CommandRequestRepository()
        {
        }

        protected override int SelectKey(CommandRequest entity)
        {
            return entity.CommandRequestID;
        }

        public override void PopulateIndexes()
        {
            CommandIDs = new PocoIndex<int, CommandRequest, string>(Cache, a => a.CommandID);
            CommandTypes = new PocoIndex<int, CommandRequest, int>(Cache, GetQueueIndex);
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
            switch ((CommandRequestType) req.CommandType)
            {
                case CommandRequestType.TvDB_DownloadImages:
                case CommandRequestType.ImageDownload:
                case CommandRequestType.ValidateAllImages:
                    return 2;
                case CommandRequestType.HashFile:
                case CommandRequestType.ReadMediaInfo:
                    return 1;
                default:
                    return 0;
            }
        }

        public override void RegenerateDb()
        {

        }

        public static CommandRequestRepository Create()
        {
            return new CommandRequestRepository();
        }

        public CommandRequest GetByCommandID(string cmdid)
        {
            if (string.IsNullOrEmpty(cmdid)) return null;
            lock (Cache)
            {
                var crs = CommandIDs.GetMultiple(cmdid);
                var cr = crs.FirstOrDefault();
                if (crs.Count <= 1) return cr;

                crs.Remove(cr);
                foreach (var crd in crs) Delete(crd);
                return cr;
            }
        }


        public CommandRequest GetNextDBCommandRequestGeneral()
        {
            try
            {
                lock (Cache)
                {
                    return CommandTypes.GetMultiple(0).OrderBy(a => a.Priority)
                        .ThenBy(a => a.DateTimeUpdated).FirstOrDefault();
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
            lock (Cache)
            {
                return CommandTypes.GetMultiple(0);
            }
        }

        public CommandRequest GetNextDBCommandRequestHasher()
        {
            try
            {
                lock (Cache)
                {
                    return CommandTypes.GetMultiple(1).OrderBy(a => a.Priority)
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
            lock (Cache)
            {
                return CommandTypes.GetMultiple(1);
            }
        }

        public CommandRequest GetNextDBCommandRequestImages()
        {
            try
            {
                lock (Cache)
                {
                    return CommandTypes.GetMultiple(2).OrderBy(a => a.Priority)
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
            lock (Cache)
            {
                return CommandTypes.GetMultiple(2);
            }
        }

        public int GetQueuedCommandCountGeneral()
        {
            lock (Cache)
            {
                return CommandTypes.GetMultiple(0).Count;
            }
        }

        public int GetQueuedCommandCountHasher()
        {
            lock (Cache)
            {
                return CommandTypes.GetMultiple(1).Count;
            }
        }

        public int GetQueuedCommandCountImages()
        {
            lock (Cache)
            {
                return CommandTypes.GetMultiple(2).Count;
            }
        }
    }
}