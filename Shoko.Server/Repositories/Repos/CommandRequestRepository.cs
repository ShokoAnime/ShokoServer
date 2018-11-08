using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.CommandQueue.Commands;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;
namespace Shoko.Server.Repositories.Repos
{
    public class CommandRequestRepository : BaseRepository<CommandRequest, string>, ICommandProvider
    {
        private PocoIndex<string, CommandRequest, string> Batches;
        private PocoIndex<string, CommandRequest, int> WorkTypes;
        private PocoIndex<string, CommandRequest, string> Classes;

        /*
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
            */
        internal override string SelectKey(CommandRequest entity) => entity.Id;


        internal override void PopulateIndexes()
        {
            Batches = new PocoIndex<string, CommandRequest, string>(Cache, a=>a.Batch);
            WorkTypes = new PocoIndex<string, CommandRequest, int>(Cache, a=>a.Type);
            Classes= new PocoIndex<string, CommandRequest, string>(Cache, a => a.Class);
        }

        internal override void ClearIndexes()
        {
    
            Batches = null;
            WorkTypes = null;
            Classes = null;
        }

        public void Clear()
        {
            ClearQueue();
        }

        public int GetQueuedCommandCount(string batch)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Batches.GetMultiple(batch).Count(a => a.ExecutionDate <= DateTime.UtcNow);
                return Table.Count(a => a.Batch==batch && a.ExecutionDate <= DateTime.UtcNow);
            }
        }
        public int GetQueuedCommandCount(params WorkTypes[] wt)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return wt.Sum(b=>WorkTypes.GetMultiple((int)b).Count(a => a.ExecutionDate <= DateTime.UtcNow));
                List<int> ints = wt.Cast<int>().ToList();
                return Table.Count(a => ints.Contains(a.Type) && a.ExecutionDate<=DateTime.UtcNow);
            }
        }
        public Dictionary<string, int> GetByClasses()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Classes.GetIndexes().ToDictionary(a => a, a => Classes.GetMultiple(a).Count);
                return Table.GroupBy(a => a.Class).ToDictionary(a => a.Key, a => a.Count());
            }
        }
        public int GetQueuedCommandCount()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Values.Count(a=>a.ExecutionDate<=DateTime.UtcNow);
                return Table.Count(a => a.ExecutionDate <= DateTime.UtcNow);
            }
        }



        public void ClearQueue(params WorkTypes[] wt)
        {
            FindAndDelete(() =>
            {
                if (IsCached)
                    return wt.SelectMany(a => WorkTypes.GetMultiple((int)a)).ToList();
                List<int> ints = wt.Cast<int>().ToList();
                return Table.Where(a => ints.Contains(a.Type)).ToList();
            });
        }
        public void ClearQueue()
        {
            FindAndDelete(() =>
            {
                if (IsCached)
                    return Cache.Values.Where(a=>a.Type!=(int)CommandQueue.Commands.WorkTypes.Schedule).ToList();
                return Table.Where(a => a.Type != (int) CommandQueue.Commands.WorkTypes.Schedule).ToList();
            });
        }

        public List<ICommand> Get(int qnty, Dictionary<string, int> tagLimits, List<string> batchLimits, List<WorkTypes> workLimits)
        {
            List<ICommand> cmds=new List<ICommand>();
            Dictionary<string, int> localLimits = tagLimits.ToDictionary(a => a.Key, a => a.Value);
            bool nomore = false;
            do
            {
                FindAndDelete(() =>
                {
                    IQueryable<CommandRequest> req = Table.Where(a => a.ExecutionDate <= DateTime.UtcNow);
                    //Filter already filled tags by the queue
                    foreach (string k in localLimits.Where(a => a.Value == 0).Select(a => a.Key))
                        req = req.Where(a => a.ParallelTag != k);
                    foreach (string s in batchLimits)
                        req = req.Where(a => a.Batch != s);
                    foreach (WorkTypes w in workLimits)
                        req = req.Where(a => a.Type != (int) w);
                    List<string> ids = req.OrderBy(a => a.Priority).Take(qnty).Select(a => a.Id).ToList();
                    if (ids.Count == 0)
                    {
                        nomore = true;
                        return new List<CommandRequest>();
                    }
                    List<CommandRequest> crs = GetMany(ids);
                    foreach (CommandRequest r in crs.ToList())
                    {
                        if (localLimits.ContainsKey(r.ParallelTag))
                        {
                            if (localLimits[r.ParallelTag] == 0)
                                crs.Remove(r); //Sorry boy, already filled
                            else
                                localLimits[r.ParallelTag]--;
                        }
                    }
                    cmds.AddRange(crs.Select(a => a.ToCommand()));
                    return crs; //This ones needs to be deleted from DB.
                });
                if (nomore)
                    break;
            } while (cmds.Count < qnty);
            return cmds;
        }

        public void Put(ICommand cmd, string batch="Server", int secondsInFuture = 0, string error=null, int retries=0)
        {
            using (var upd = BeginAddOrUpdate(() => GetByID(cmd.Id), cmd.ToCommandRequest))
            {
                if (upd.IsUpdate)
                    return;
                upd.Entity.Batch = batch;
                upd.Entity.ExecutionDate = DateTime.UtcNow.AddSeconds(secondsInFuture);
                upd.Entity.LastError = error;
                upd.Entity.Retries = retries;
                upd.Commit();
            }
        }

        public void PutRange(IEnumerable<ICommand> cmds, string batch = "Server", int secondsInFuture = 0)
        {
            DateTime n = DateTime.UtcNow.AddSeconds(secondsInFuture);
            using (var upd = BeginBatchUpdate(() => GetMany(cmds.Select(a => a.Id))))
            {
                foreach (ICommand cmd in cmds)
                {
                    CommandRequest r=upd.Find(a => a.Id == cmd.Id);
                    if (r == null)
                    {
                        r = upd.Create(cmd.ToCommandRequest());
                        r.Batch = batch;
                        r.ExecutionDate = n;
                        r.Retries = 0;
                    }
                }
                upd.Commit();
            }
        }

        public void ClearBatch(string batch)
        {
            FindAndDelete(() =>
            {
                if (IsCached)
                    return Batches.GetMultiple(batch);
                return Table.Where(a => a.Batch==batch).ToList();
            });
        }

        public void ClearWorkTypes(params WorkTypes[] worktypes)
        {
            ClearQueue(worktypes);
        }

        /*


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
            using (RepoLock.ReaderLock())
            {
                return IsCached ? CommandIDs.GetOne(cmdid) : Table.FirstOrDefault(a => a.CommandID == cmdid);
            }

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

        */
    }
}