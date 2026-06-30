using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NutzCode.InMemoryIndex;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_File_EpisodeRepository : BaseCachedRepository<CrossRef_File_Episode, int>
{
    private IQueueScheduler? _scheduler;

    private PocoIndex<int, CrossRef_File_Episode, string>? _ed2k;

    private PocoIndex<int, CrossRef_File_Episode, int>? _anidbAnimeIDs;

    private PocoIndex<int, CrossRef_File_Episode, int>? _anidbEpisodeIDs;

    public CrossRef_File_EpisodeRepository(ILogger<CrossRef_File_EpisodeRepository> logger, IServiceProvider serviceProvider, DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        EndSaveCallback = obj =>
        {
            _scheduler ??= serviceProvider.GetRequiredService<IQueueScheduler>();
            _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = obj.AnimeID).GetAwaiter().GetResult();
        };
        EndDeleteCallback = obj =>
        {
            if (obj is not { AnimeID: > 0 }) return;

            logger.LogTrace("Updating group stats by anime from CrossRef_File_EpisodeRepository.Delete: {AnimeID}", obj.AnimeID);
            _scheduler ??= serviceProvider.GetRequiredService<IQueueScheduler>();
            _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = obj.AnimeID).GetAwaiter().GetResult();
        };
    }

    protected override int SelectKey(CrossRef_File_Episode entity)
        => entity.CrossRef_File_EpisodeID;

    public override void PopulateIndexes()
    {
        _ed2k = Cache.CreateIndex(a => a.Hash);
        _anidbAnimeIDs = Cache.CreateIndex(a => a.AnimeID);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.EpisodeID);
    }

    public virtual IReadOnlyList<CrossRef_File_Episode> GetByEd2k(string ed2k)
        => _ed2k!.GetMultiple(ed2k).OrderBy(a => a.EpisodeOrder).ToList();

    public IReadOnlyList<CrossRef_File_Episode> GetByAnimeID(int animeID)
        => _anidbAnimeIDs!.GetMultiple(animeID);

    public IReadOnlyList<CrossRef_File_Episode> GetByEpisodeID(int episodeID)
        => _anidbEpisodeIDs!.GetMultiple(episodeID);
}
