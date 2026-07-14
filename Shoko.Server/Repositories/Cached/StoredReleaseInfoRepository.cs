using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Release;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Models.Release;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Cached;

public class StoredReleaseInfoRepository : BaseCachedRepository<StoredReleaseInfo, int>
{
    private IQueueScheduler? _scheduler;

    public StoredReleaseInfoRepository(DatabaseFactory databaseFactory, IServiceProvider serviceProvider) : base(databaseFactory)
    {
        EndSaveCallback = obj =>
        {
            _scheduler ??= serviceProvider.GetRequiredService<IQueueScheduler>();
            foreach (var animeID in obj.CrossReferences.Select(x => x.AnidbAnimeID).WhereNotNull().Distinct())
                _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = animeID).GetAwaiter().GetResult();
        };
        EndDeleteCallback = obj =>
        {
            _scheduler ??= serviceProvider.GetRequiredService<IQueueScheduler>();
            foreach (var animeID in obj.CrossReferences.Select(x => x.AnidbAnimeID).WhereNotNull().Distinct())
                _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = animeID).GetAwaiter().GetResult();
        };
    }
    private PocoIndex<int, StoredReleaseInfo, string>? _ed2k;
    private PocoIndex<int, StoredReleaseInfo, (string groupId, string source)>? _groupIDs;
    private PocoIndex<int, StoredReleaseInfo, string?>? _releaseURIs;
    private PocoIndex<int, StoredReleaseInfo, int>? _anidbEpisodeIDs;
    private PocoIndex<int, StoredReleaseInfo, int>? _anidbAnimeIDs;

    protected override int SelectKey(StoredReleaseInfo entity)
        => entity.StoredReleaseInfoID;

    public override void PopulateIndexes()
    {
        _ed2k = Cache.CreateIndex(a => a.ED2K);
        _groupIDs = Cache.CreateIndex(a => (a.GroupID ?? string.Empty, a.GroupSource ?? string.Empty));
        _releaseURIs = Cache.CreateIndex(a => a.ReleaseURI);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.CrossReferences.Select(b => b.AnidbEpisodeID).Where(id => id > 0));
        _anidbAnimeIDs = Cache.CreateIndex(a => a.CrossReferences.Select(b => b.AnidbAnimeID).WhereNotNull().Distinct());
    }

    public IReadOnlyList<StoredReleaseInfo> GetByEd2k(string ed2k)
        => !string.IsNullOrWhiteSpace(ed2k)
            ? _ed2k!.GetMultiple(ed2k)
            : [];

    public virtual StoredReleaseInfo? GetByEd2kAndFileSize(string ed2k, long fileSize)
        => GetByEd2k(ed2k).FirstOrDefault(a => a.FileSize == fileSize);

    public IReadOnlyList<StoredReleaseInfo> GetByGroupAndProviderIDs(string groupId, string source)
        => !string.IsNullOrEmpty(groupId) && !string.IsNullOrEmpty(source)
            ? _groupIDs!.GetMultiple((groupId, source))
            : [];

    public StoredReleaseInfo? GetByReleaseURI(string? releaseUri)
        => !string.IsNullOrEmpty(releaseUri)
            ? _releaseURIs!.GetOne(releaseUri)
            : null;

    public IReadOnlyList<StoredReleaseInfo> GetByAnidbEpisodeID(int episodeId)
        => episodeId > 0
            ? _anidbEpisodeIDs!.GetMultiple(episodeId)
            : [];

    public IReadOnlyList<StoredReleaseInfo> GetByAnidbAnimeID(int animeId)
        => animeId > 0
            ? _anidbAnimeIDs!.GetMultiple(animeId)
            : [];

    public IReadOnlyList<IReleaseGroup> GetReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.Source: "AniDB" } releaseInfo ? releaseInfo.Group : null)
            .WhereNotNull()
            .DistinctBy(g => (g.ID, g.Source))
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();

    public IReadOnlyList<IReleaseGroup> GetUsedReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.Source: "AniDB" } releaseInfo && RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize) is { } ? releaseInfo.Group : null)
            .WhereNotNull()
            .DistinctBy(g => (g.ID, g.Source))
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();

    public IReadOnlyList<IReleaseGroup> GetUnusedReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.Source: "AniDB" } releaseInfo && RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize) is not { } ? releaseInfo.Group : null)
            .WhereNotNull()
            .DistinctBy(g => (g.ID, g.Source))
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();
}
