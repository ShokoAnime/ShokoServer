using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct;

public class CommandRequestRepository : BaseDirectRepository<CommandRequest, int>
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static readonly HashSet<int> CommandTypesHasher = new()
    {
        (int)CommandRequestType.HashFile, (int)CommandRequestType.ReadMediaInfo
    };
    private static readonly int[] CommandTypesHasherArray = CommandTypesHasher.ToArray();

    private static readonly HashSet<int> CommandTypesImages = new()
    {
        (int)CommandRequestType.TvDB_DownloadImages,
        (int)CommandRequestType.ImageDownload,
        (int)CommandRequestType.ValidateAllImages,
        (int)CommandRequestType.DownloadAniDBImages
    };
    private static readonly int[] CommandTypesImagesArray = CommandTypesImages.ToArray();

    private static readonly HashSet<int> AniDbUdpCommands = new()
    {
        (int)CommandRequestType.ProcessFile,
        (int)CommandRequestType.AniDB_AddFileUDP,
        (int)CommandRequestType.AniDB_DeleteFileUDP,
        (int)CommandRequestType.AniDB_GetCalendar,
        (int)CommandRequestType.AniDB_GetEpisodeUDP,
        (int)CommandRequestType.AniDB_GetFileUDP,
        (int)CommandRequestType.AniDB_GetMyListFile,
        (int)CommandRequestType.AniDB_GetReleaseGroup,
        (int)CommandRequestType.AniDB_GetReleaseGroupStatus,
        (int)CommandRequestType.AniDB_GetReviews, // this isn't used.
        (int)CommandRequestType.AniDB_GetUpdated,
        (int)CommandRequestType.AniDB_UpdateWatchedUDP,
        (int)CommandRequestType.AniDB_UpdateMylistStats,
        (int)CommandRequestType.AniDB_VoteAnime
    };

    private static readonly HashSet<int> AniDbHttpCommands = new()
    {
        (int)CommandRequestType.AniDB_GetAnimeHTTP,
        (int)CommandRequestType.AniDB_SyncMyList,
        (int)CommandRequestType.AniDB_SyncVotes
    };

    // This is called very often, so speed it up as much as possible
    // We can spare bytes of RAM to speed up the command queue
    private static readonly HashSet<int> CommandTypesGeneral = Enum.GetValues(typeof(CommandRequestType))
        .OfType<CommandRequestType>().Cast<int>().Except(CommandTypesHasher).Except(CommandTypesImages)
        .ToHashSet();
    private static readonly int[] CommandTypesGeneralArray = CommandTypesGeneral.ToArray();

    private static readonly HashSet<int> CommandTypesGeneralUDPBan = CommandTypesGeneral.Except(AniDbUdpCommands).ToHashSet();
    private static readonly int[] CommandTypesGeneralUDPBanArray = CommandTypesGeneralUDPBan.ToArray();

    private static readonly HashSet<int> CommandTypesGeneralHTTPBan = CommandTypesGeneral.Except(AniDbHttpCommands).ToHashSet();
    private static readonly int[] CommandTypesGeneralHTTPBanArray = CommandTypesGeneralHTTPBan.ToArray();

    private static readonly HashSet<int> CommandTypesGeneralFullBan = CommandTypesGeneral.Except(AniDbUdpCommands).Except(AniDbHttpCommands).ToHashSet();
    private static readonly int[] CommandTypesGeneralFullBanArray = CommandTypesGeneralFullBan.ToArray();

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
        if (CommandTypesImages.Contains(req.CommandType)) return 2;
        if (CommandTypesHasher.Contains(req.CommandType)) return 1;
        return 0;
    }

    public CommandRequest GetByCommandID(string cmdid)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var crs = Lock(session, s => s.Query<CommandRequest>()
            .Where(a => a.CommandID == cmdid).ToList());
        var cr = crs.FirstOrDefault();
        if (crs.Count <= 1) return cr;

        crs.RemoveAt(0);
        foreach (var crd in crs) Delete(crd);

        return cr;
    }

    public bool CheckIfCommandRequestIsDisabled(CommandRequestType type, bool httpBanned, bool udpBanned, bool udpUnavailable)
    {
        if (!CommandTypesGeneral.Contains((int)type))
            return false;

        if (httpBanned && udpBanned)
            return !CommandTypesGeneralFullBan.Contains((int)type);

        if (udpBanned)
            return !CommandTypesGeneralUDPBan.Contains((int)type);

        if (httpBanned)
            return !CommandTypesGeneralHTTPBan.Contains((int)type);

        return false;
    }

    public List<CommandRequest> GetNextGeneralCommandRequests(IUDPConnectionHandler udpHandler, IHttpConnectionHandler httpHandler, bool showAll = false)
    {
        try
        {
            var types = CommandTypesGeneralArray;
            if (!showAll) {
                var noUDP = udpHandler.IsBanned || !udpHandler.IsNetworkAvailable;
                if (httpHandler.IsBanned && noUDP) types = CommandTypesGeneralFullBanArray;
                else if (noUDP) types = CommandTypesGeneralUDPBanArray;
                else if (httpHandler.IsBanned) types = CommandTypesGeneralHTTPBanArray;
            }
            var cr = Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .ToList();
            });

            return cr;
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next commands for the General Queue: {e}");
            return null;
        }
    }

    public CommandRequest GetNextDBCommandRequestGeneral(IUDPConnectionHandler udpHandler, IHttpConnectionHandler httpHandler)
    {
        try
        {
            var noUDP = udpHandler.IsBanned || !udpHandler.IsNetworkAvailable;
            var types = CommandTypesGeneralArray;
            if (httpHandler.IsBanned && noUDP) types = CommandTypesGeneralFullBanArray;
            else if (noUDP) types = CommandTypesGeneralUDPBanArray;
            else if (httpHandler.IsBanned) types = CommandTypesGeneralHTTPBanArray;

            var cr = Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .Take(1)
                    .SingleOrDefault();
            });

            return cr;
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next command for the General Queue: {e}");
            return null;
        }
    }

    public List<CommandRequest> GetNextHasherCommandRequests()
    {
        try
        {
            var types = CommandTypesHasherArray;
            var cr = Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .ToList();
            });

            return cr;
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next commands for the Hasher Queue: {e}");
            return null;
        }
    }

    public CommandRequest GetNextDBCommandRequestHasher()
    {
        try
        {
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                var crs = session.Query<CommandRequest>()
                    .Where(a => CommandTypesHasherArray.Contains(a.CommandType))
                    .OrderBy(cr => cr.Priority)
                    .ThenBy(cr => cr.DateTimeUpdated)
                    .Take(1)
                    .SingleOrDefault();
                return crs;
            });
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next command for the Hasher Queue: {e}");
            return null;
        }
    }


    public List<CommandRequest> GetNextImagesCommandRequests()
    {
        try
        {
            var types = CommandTypesImagesArray;
            var cr = Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .ToList();
            });

            return cr;
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next commands for the Image Queue: {e}");
            return null;
        }
    }

    public CommandRequest GetNextDBCommandRequestImages()
    {
        try
        {
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                var crs = session.Query<CommandRequest>()
                    .Where(a => CommandTypesImagesArray.Contains(a.CommandType))
                    .OrderBy(cr => cr.Priority)
                    .ThenBy(cr => cr.DateTimeUpdated)
                    .Take(1)
                    .SingleOrDefault();
                return crs;
            });
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next command for the Image Queue: {e}");
            return null;
        }
    }

    public int GetQueuedCommandCountByType(string queueType)
    {
        return queueType.ToLowerInvariant() switch {
            "general" => GetQueuedCommandCountGeneral(),
            "hasher" => GetQueuedCommandCountHasher(),
            "image" => GetQueuedCommandCountImages(),
            _ => 0,
        };
    }

    public int GetQueuedCommandCountGeneral()
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var crs = session.Query<CommandRequest>()
                .Count(a => CommandTypesGeneralArray.Contains(a.CommandType));

            return crs;
        });
    }

    public int GetQueuedCommandCountHasher()
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var crs = session.Query<CommandRequest>()
                .Count(a => CommandTypesHasherArray.Contains(a.CommandType));

            return crs;
        });
    }

    public int GetQueuedCommandCountImages()
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var crs = session.Query<CommandRequest>()
                .Count(a => CommandTypesImagesArray.Contains(a.CommandType));

            return crs;
        });
    }

    public void ClearByQueueType(string queueType)
    {
        switch (queueType.ToLowerInvariant()) {
            case "general":
                ClearGeneralQueue();
                break;
            case "hasher":
                ClearHasherQueue();
                break;
            case "image":
                ClearImageQueue();
                break;
        };
    }

    public void ClearGeneralQueue()
    {
        Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var currentCommand = ShokoService.CmdProcessorGeneral.CurrentCommand;
            using var transaction = session.BeginTransaction();
            if (currentCommand != null)
            {
                var currentID = currentCommand.CommandRequestID;
                session.Query<CommandRequest>().Where(a => CommandTypesGeneralArray.Contains(a.CommandType) && a.CommandRequestID != currentID).Delete();
            }
            else
            {
                session.Query<CommandRequest>().Where(a => CommandTypesGeneralArray.Contains(a.CommandType)).Delete();
            }

            transaction.Commit();
        });
    }

    public void ClearHasherQueue()
    {
        Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var currentCommand = ShokoService.CmdProcessorHasher.CurrentCommand;
            using var transaction = session.BeginTransaction();
            if (currentCommand != null)
            {
                var currentID = currentCommand.CommandRequestID;
                session.Query<CommandRequest>().Where(a => CommandTypesHasherArray.Contains(a.CommandType) && a.CommandRequestID != currentID).Delete();
            }
            else
            {
                session.Query<CommandRequest>().Where(a => CommandTypesHasherArray.Contains(a.CommandType)).Delete();
            }

            transaction.Commit();
        });
    }

    public void ClearImageQueue()
    {
        Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var currentCommand = ShokoService.CmdProcessorImages.CurrentCommand;
            using var transaction = session.BeginTransaction();
            if (currentCommand != null)
            {
                var currentID = currentCommand.CommandRequestID;
                session.Query<CommandRequest>().Where(a => CommandTypesImagesArray.Contains(a.CommandType) && a.CommandRequestID != currentID).Delete();
            }
            else
            {
                session.Query<CommandRequest>().Where(a => CommandTypesImagesArray.Contains(a.CommandType)).Delete();
            }

            transaction.Commit();
        });
    }
}
