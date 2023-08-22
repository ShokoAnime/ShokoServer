using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using NLog;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct;

public class CommandRequestRepository : BaseDirectRepository<CommandRequest, int>
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static readonly HashSet<int> CommandTypesHasher = new()
    {
        (int)CommandRequestType.HashFile,
        (int)CommandRequestType.ReadMediaInfo,
        (int)CommandRequestType.AVDumpFile,
    };
    private static readonly int[] CommandTypesHasherArray = CommandTypesHasher.ToArray();

    private static readonly HashSet<int> CommandTypesImages = new()
    {
        (int)CommandRequestType.TvDB_DownloadImages,
        (int)CommandRequestType.ImageDownload,
        (int)CommandRequestType.ValidateAllImages,
        (int)CommandRequestType.DownloadAniDBImages,
    };
    private static readonly int[] CommandTypesImagesArray = CommandTypesImages.ToArray();

    private static readonly HashSet<int> CommandTypesGeneral = Enum
        .GetValues(typeof(CommandRequestType))
        .OfType<CommandRequestType>()
        .Cast<int>()
        .Except(CommandTypesHasher)
        .Except(CommandTypesImages)
        .ToHashSet();
    private static readonly int[] CommandTypesGeneralArray = CommandTypesGeneral.ToArray();

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
        (int)CommandRequestType.AniDB_VoteAnime,
        (int)CommandRequestType.AniDB_VoteEpisode,
    };

    private static readonly HashSet<int> AniDbHttpCommands = new()
    {
        (int)CommandRequestType.AniDB_GetAnimeHTTP_Force,
        (int)CommandRequestType.AniDB_SyncMyList,
        (int)CommandRequestType.AniDB_SyncVotes,
    };

    private static readonly HashSet<int> HttpNetworkCommands = new()
    {
        // Hasher commands
        (int)CommandRequestType.AVDumpFile,

        // General commands
        (int)CommandRequestType.TvDBSearch,
        (int)CommandRequestType.TvDB_UpdateSeries,
        (int)CommandRequestType.TvDB_SearchAnime,
        (int)CommandRequestType.MovieDB_SearchAnime,
        (int)CommandRequestType.Trakt_SearchAnime,
        (int)CommandRequestType.Trakt_UpdateInfo,
        (int)CommandRequestType.Trakt_EpisodeHistory,
        (int)CommandRequestType.Trakt_SyncCollection,
        (int)CommandRequestType.Trakt_SyncCollectionSeries,
        (int)CommandRequestType.Trakt_EpisodeCollection,
        (int)CommandRequestType.Trakt_UpdateAllSeries,
        (int)CommandRequestType.Plex_Sync,
        (int)CommandRequestType.TvDB_UpdateEpisode, // this isn't used.

        // Image commands
        (int)CommandRequestType.TvDB_DownloadImages,
        (int)CommandRequestType.ImageDownload,
        (int)CommandRequestType.ValidateAllImages,
        (int)CommandRequestType.DownloadAniDBImages,
    };

    /// <summary>
    /// A map of commands to use under different conditions.
    /// </summary>
    private static readonly ConcurrentDictionary<string, int[]> CommandConditionMap = new();

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

    private int[] GetExecutableCommands(IEnumerable<int> commands, IConnectivityService connectivityService)
    {
        // This caching takes advantage over the fact that all the base arrays have an unique length.
        var httpBanned = connectivityService.IsAniDBHttpBanned;
        var networkUnavailable = !connectivityService.NetworkAvailability.HasInternet();
        var udpBanned = connectivityService.IsAniDBUdpBanned;
        var udpUnavailable = !connectivityService.IsAniDBUdpReachable;
        var count = commands is int[] countArray ? countArray.Length : commands.Count();
        var key = $"c={count},nu={networkUnavailable},hb={httpBanned},ub={udpBanned},uu={udpUnavailable}";
        if (CommandConditionMap.TryGetValue(key, out var array))
            return array;

        if (udpBanned || udpUnavailable || networkUnavailable)
        {
            _logger.Trace($"Filtering UDP commands. {nameof(udpBanned)}: {udpBanned}, {nameof(udpUnavailable)}: {udpUnavailable}, {nameof(networkUnavailable)}: {networkUnavailable}");
            commands = commands.Except(AniDbUdpCommands);
        }

        if (httpBanned || networkUnavailable)
        {
            _logger.Trace($"Filtering HTTP commands. {nameof(httpBanned)}: {httpBanned}, {nameof(networkUnavailable)}: {networkUnavailable}");
            commands = commands.Except(AniDbHttpCommands);
        }

        if (networkUnavailable)
        {
            _logger.Trace($"Filtering Web commands. {nameof(networkUnavailable)}: {networkUnavailable}");
            commands = commands.Except(HttpNetworkCommands);
        }

        array = commands as int[] ?? commands.ToArray();
        CommandConditionMap.TryAdd(key, array);
        return array;
    }

    public bool CheckIfCommandRequestIsDisabled(CommandRequestType type, IConnectivityService connectivityService)
    {
        if (!CommandTypesGeneral.Contains((int)type))
            return false;

        var udpBanned = connectivityService.IsAniDBUdpBanned;
        var networkUnavailable = !connectivityService.NetworkAvailability.HasInternet();
        var udpUnavailable = !connectivityService.IsAniDBUdpReachable;
        if ((udpBanned || udpUnavailable || networkUnavailable) && AniDbUdpCommands.Contains((int)type))
            return true;

        var httpBanned = connectivityService.IsAniDBHttpBanned;
        if ((httpBanned || networkUnavailable) && AniDbHttpCommands.Contains((int)type))
            return true;

        if (networkUnavailable && HttpNetworkCommands.Contains((int)type))
            return true;

        return false;
    }

    public List<CommandRequest> GetNextGeneralCommandRequests(IConnectivityService connectivityService, bool showAll = false)
    {
        try
        {
            var types = showAll ? CommandTypesGeneralArray : GetExecutableCommands(CommandTypesGeneralArray, connectivityService);
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .ToList();
            });
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next commands for the General Queue: {e}");
            return null;
        }
    }

    public CommandRequest GetNextDBCommandRequestGeneral(IConnectivityService connectivityService)
    {
        try
        {
            var types = GetExecutableCommands(CommandTypesGeneralArray, connectivityService);
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .Take(1)
                    .SingleOrDefault();
            });
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next command for the General Queue: {e}");
            return null;
        }
    }

    public List<CommandRequest> GetNextHasherCommandRequests(IConnectivityService connectivityService, bool showAll)
    {
        try
        {
            var types = showAll ? CommandTypesHasherArray : GetExecutableCommands(CommandTypesHasherArray, connectivityService);
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .ToList();
            });
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next commands for the Hasher Queue: {e}");
            return null;
        }
    }

    public CommandRequest GetNextDBCommandRequestHasher(IConnectivityService connectivityService)
    {
        try
        {
            var types = GetExecutableCommands(CommandTypesHasherArray, connectivityService);
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(cr => cr.Priority)
                    .ThenBy(cr => cr.DateTimeUpdated)
                    .Take(1)
                    .SingleOrDefault();
            });
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next command for the Hasher Queue: {e}");
            return null;
        }
    }


    public List<CommandRequest> GetNextImagesCommandRequests(IConnectivityService connectivityService, bool showAll)
    {
        try
        {
            var types = showAll ? CommandTypesImagesArray : GetExecutableCommands(CommandTypesImagesArray, connectivityService);
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.DateTimeUpdated)
                    .ToList();
            });
        }
        catch (Exception e)
        {
            _logger.Error($"There was an error retrieving the next commands for the Image Queue: {e}");
            return null;
        }
    }

    public CommandRequest GetNextDBCommandRequestImages(IConnectivityService connectivityService)
    {
        try
        {
            var types = GetExecutableCommands(CommandTypesImagesArray, connectivityService);
            return Lock(() =>
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                return session.Query<CommandRequest>()
                    .Where(a => types.Contains(a.CommandType))
                    .OrderBy(cr => cr.Priority)
                    .ThenBy(cr => cr.DateTimeUpdated)
                    .Take(1)
                    .SingleOrDefault();
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
