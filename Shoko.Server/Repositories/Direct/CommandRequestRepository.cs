using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct
{
    public class CommandRequestRepository : BaseDirectRepository<CommandRequest, int>
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

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
            .OfType<CommandRequestType>().Select(a => (int) a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .ToHashSet();

        private static readonly HashSet<int> CommandTypesGeneralUDPBan = Enum.GetValues(typeof(CommandRequestType))
            .OfType<CommandRequestType>().Select(a => (int) a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .Except(AniDbUdpCommands).ToHashSet();

        private static readonly HashSet<int> CommandTypesGeneralHTTPBan = Enum.GetValues(typeof(CommandRequestType))
            .OfType<CommandRequestType>().Select(a => (int) a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .Except(AniDbHttpCommands).ToHashSet();

        private static readonly HashSet<int> CommandTypesGeneralFullBan = Enum.GetValues(typeof(CommandRequestType))
            .OfType<CommandRequestType>().Select(a => (int) a).Except(CommandTypesHasher).Except(CommandTypesImages)
            .Except(AniDbUdpCommands).Except(AniDbHttpCommands).ToHashSet();

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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByCommandID(session, cmdid);
            }
        }

        public CommandRequest GetByCommandID(ISession session, string cmdid)
        {
            var crs = session
                .CreateCriteria(typeof(CommandRequest))
                .Add(Restrictions.Eq("CommandID", cmdid))
                .List<CommandRequest>().ToList();
            var cr = crs.FirstOrDefault();
            if (crs.Count <= 1) return cr;

            crs.Remove(cr);
            foreach (var crd in crs) Delete(crd);
            return cr;
        }


        public CommandRequest GetNextDBCommandRequestGeneral()
        {
            try
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                var types = CommandTypesGeneral;
                var udpHandler = ShokoServer.ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
                var httpHandler = ShokoServer.ServiceContainer.GetRequiredService<IHttpConnectionHandler>();
                var noUDP = udpHandler.IsBanned || !udpHandler.IsNetworkAvailable;
                // This is called very often, so speed it up as much as possible
                // We can spare bytes of RAM to speed up the command queue
                if (httpHandler.IsBanned && noUDP)
                {
                    types = CommandTypesGeneralFullBan;
                }
                else if (noUDP)
                {
                    types = CommandTypesGeneralUDPBan;
                }
                else if (httpHandler.IsBanned)
                {
                    types = CommandTypesGeneralHTTPBan;
                }
                //don't need all rows, just first
                var cr = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(field => field.CommandType)
                    .IsIn(types.ToArray())
                    .OrderBy(r => r.Priority).Asc
                    .ThenBy(r => r.DateTimeUpdated).Asc
                    .Take(1)
                    .SingleOrDefault<CommandRequest>();

                return cr;
            }
            catch (Exception e)
            {
                logger.Error($"There was an error retrieving the next command for the General Queue: {e}");
                return null;
            }
        }

        public List<CommandRequest> GetAllCommandRequestGeneral()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                // This is used to clear the queue, we don't need order
                var crs = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(field => field.CommandType).IsIn(CommandTypesGeneral.ToArray())
                    .List<CommandRequest>().ToList();

                return crs;
            }
        }

        public CommandRequest GetNextDBCommandRequestHasher()
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    var crs = session.QueryOver<CommandRequest>()
                        .WhereRestrictionOn(field => field.CommandType).IsIn(CommandTypesHasher.ToArray())
                        .OrderBy(cr => cr.Priority).Asc
                        .ThenBy(cr => cr.DateTimeUpdated).Asc
                        .Take(1)
                        .List<CommandRequest>();
                    if (crs.Count > 0) return crs[0];

                    return null;
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(field => field.CommandType).IsIn(CommandTypesHasher.ToArray())
                    .OrderBy(cr => cr.Priority).Asc
                    .ThenBy(cr => cr.DateTimeUpdated).Asc
                    .List<CommandRequest>().ToList();

                return crs;
            }
        }

        public CommandRequest GetNextDBCommandRequestImages()
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    var crs = session.QueryOver<CommandRequest>()
                        .WhereRestrictionOn(field => field.CommandType).IsIn(CommandTypesImages.ToArray())
                        .OrderBy(cr => cr.Priority).Asc
                        .ThenBy(cr => cr.DateTimeUpdated).Asc
                        .Take(1)
                        .List<CommandRequest>();
                    if (crs.Count > 0) return crs[0];

                    return null;
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(field => field.CommandType).IsIn(CommandTypesImages.ToArray())
                    .OrderBy(cr => cr.Priority).Asc
                    .ThenBy(cr => cr.DateTimeUpdated).Asc
                    .List<CommandRequest>().ToList();

                return crs;
            }
        }

        public int GetQueuedCommandCountGeneral()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(f => f.CommandType)
                    .IsIn(CommandTypesGeneral.ToArray())
                    .RowCount();

                return crs;
            }
        }

        public int GetQueuedCommandCountHasher()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(field => field.CommandType).IsIn(CommandTypesHasher.ToArray())
                    .RowCount();

                return crs;
            }
        }

        public int GetQueuedCommandCountImages()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(field => field.CommandType).IsIn(CommandTypesImages.ToArray())
                    .RowCount();

                return crs;
            }
        }

        public List<CommandRequest> GetByCommandTypes(int[] types)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session.QueryOver<CommandRequest>()
                    .WhereRestrictionOn(field => field.CommandType).IsIn(types)
                    .OrderBy(cr => cr.Priority).Asc
                    .ThenBy(cr => cr.DateTimeUpdated).Asc
                    .List<CommandRequest>().ToList();

                return crs;
            }
        }

        public void ClearGeneralQueue()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var currentCommand = ShokoService.CmdProcessorGeneral.CurrentCommand;
                using (var transaction = session.BeginTransaction())
                {
                    if (currentCommand != null)
                    {
                        session
                            .CreateSQLQuery(
                                "DELETE FROM CommandRequest WHERE CommandRequestID != :currentid AND CommandType IN (:comtypes)")
                            .SetInt32("currentid", currentCommand.CommandRequestID)
                            .SetParameterList("comtypes", CommandTypesGeneral).ExecuteUpdate();
                    }
                    else
                    {
                        session
                            .CreateSQLQuery(
                                "DELETE FROM CommandRequest WHERE CommandType IN (:comtypes)")
                            .SetParameterList("comtypes", CommandTypesGeneral).ExecuteUpdate();
                    }
                    transaction.Commit();
                }
            }

            ShokoService.CmdProcessorGeneral.QueueCount = GetQueuedCommandCountGeneral();
        }

        public void ClearHasherQueue()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var currentCommand = ShokoService.CmdProcessorHasher.CurrentCommand;
                using (var transaction = session.BeginTransaction())
                {
                    if (currentCommand != null)
                    {
                        session
                            .CreateSQLQuery(
                                "DELETE FROM CommandRequest WHERE CommandRequestID != :currentid AND CommandType IN (:comtypes)")
                            .SetInt32("currentid", currentCommand.CommandRequestID)
                            .SetParameterList("comtypes", CommandTypesHasher).ExecuteUpdate();
                    }
                    else
                    {
                        session
                            .CreateSQLQuery(
                                "DELETE FROM CommandRequest WHERE CommandType IN (:comtypes)")
                            .SetParameterList("comtypes", CommandTypesHasher).ExecuteUpdate();
                    }
                    transaction.Commit();
                }
            }

            ShokoService.CmdProcessorHasher.QueueCount = GetQueuedCommandCountHasher();
        }

        public void ClearImageQueue()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var currentCommand = ShokoService.CmdProcessorImages.CurrentCommand;
                using (var transaction = session.BeginTransaction())
                {
                    if (currentCommand != null)
                    {
                        session
                            .CreateSQLQuery(
                                "DELETE FROM CommandRequest WHERE CommandRequestID != :currentid AND CommandType IN (:comtypes)")
                            .SetInt32("currentid", currentCommand.CommandRequestID)
                            .SetParameterList("comtypes", CommandTypesImages).ExecuteUpdate();
                    }
                    else
                    {
                        session
                            .CreateSQLQuery(
                                "DELETE FROM CommandRequest WHERE CommandType IN (:comtypes)")
                            .SetParameterList("comtypes", CommandTypesImages).ExecuteUpdate();
                    }
                    transaction.Commit();
                }
            }

            ShokoService.CmdProcessorImages.QueueCount = GetQueuedCommandCountImages();
        }
    }
}
