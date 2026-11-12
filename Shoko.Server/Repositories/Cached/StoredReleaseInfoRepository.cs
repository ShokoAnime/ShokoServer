using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Models.Release;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class StoredReleaseInfoRepository(DatabaseFactory databaseFactory, IServiceProvider serviceProvider) : BaseCachedRepository<StoredReleaseInfo, int>(databaseFactory)
{
    private IQueueScheduler? _scheduler;

    protected override void OnEndSave(StoredReleaseInfo obj)
    {
        _scheduler ??= serviceProvider.GetRequiredService<IQueueScheduler>();
        foreach (var animeID in obj.CrossReferences.Select(x => x.AnidbAnimeID).WhereNotNull().Distinct())
            _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = animeID).GetAwaiter().GetResult();
    }

    protected override void OnEndDelete(StoredReleaseInfo obj)
    {
        _scheduler ??= serviceProvider.GetRequiredService<IQueueScheduler>();
        foreach (var animeID in obj.CrossReferences.Select(x => x.AnidbAnimeID).WhereNotNull().Distinct())
            _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = animeID).GetAwaiter().GetResult();
    }
    private PocoIndex<int, StoredReleaseInfo, string>? _ed2k;
    private PocoIndex<int, StoredReleaseInfo, (string groupId, string source)>? _groupIDs;
    private PocoIndex<int, StoredReleaseInfo, string?>? _releaseURIs;
    private PocoIndex<int, StoredReleaseInfo, MetadataGuid>? _metadataIDs;

    protected override int SelectKey(StoredReleaseInfo entity)
        => entity.StoredReleaseInfoID;

    public override void PopulateIndexes()
    {
        _ed2k = Cache.CreateIndex(a => a.ED2K);
        _groupIDs = Cache.CreateIndex(a => (a.GroupID ?? string.Empty, a.GroupSource ?? string.Empty));
        _releaseURIs = Cache.CreateIndex(a => a.ReleaseURI);
        _metadataIDs = Cache.CreateIndex(a => a.CrossReferences.SelectMany(b => b.IDs).Distinct());
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

    public IReadOnlyList<StoredReleaseInfo> GetAllByReleaseURI(string? releaseUri)
        => !string.IsNullOrEmpty(releaseUri)
            ? _releaseURIs!.GetMultiple(releaseUri)
            : [];

    /// <summary>
    ///   Gets the releases with a cross-reference naming an entry, of any
    ///   source.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <returns>The releases.</returns>
    public IReadOnlyList<StoredReleaseInfo> GetByMetadataID(MetadataGuid id)
        => _metadataIDs!.GetMultiple(id);

    public IReadOnlyList<StoredReleaseInfo> GetByAnidbEpisodeID(int episodeId)
        => episodeId > 0
            ? GetByMetadataID(new(MetadataSource.AniDB, MetadataEntityType.Episode, episodeId.ToString()))
            : [];

    public IReadOnlyList<StoredReleaseInfo> GetByAnidbAnimeID(int animeId)
        => animeId > 0
            ? GetByMetadataID(new(MetadataSource.AniDB, MetadataEntityType.Series, animeId.ToString()))
            : [];

    public IReadOnlyList<IReleaseGroup> GetReleaseGroups(IReadOnlySet<string>? sources)
        => GetReleaseGroupsCore(sources, null);

    public IReadOnlyList<IReleaseGroup> GetUsedReleaseGroups(IReadOnlySet<string>? sources)
        => GetReleaseGroupsCore(sources, true);

    public IReadOnlyList<IReleaseGroup> GetUnusedReleaseGroups(IReadOnlySet<string>? sources)
        => GetReleaseGroupsCore(sources, false);

    private IReadOnlyList<IReleaseGroup> GetReleaseGroupsCore(IReadOnlySet<string>? sources, bool? isUsed)
        => GetAll()
            .Select(a => a is IReleaseInfo { Group: { } group }
                && (sources is null || sources.Contains(group.Source))
                && (isUsed is null || RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize) is { } == isUsed.Value)
                ? group
                : null)
            .WhereNotNull()
            .DistinctBy(g => (g.ID, g.Source))
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();
}
