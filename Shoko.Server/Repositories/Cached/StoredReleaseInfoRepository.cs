using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Databases;
using Shoko.Server.Models.Release;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class StoredReleaseInfoRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<StoredReleaseInfo, int>(databaseFactory)
{
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
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.CrossReferences.Select(b => b.AnidbEpisodeID));
        _anidbAnimeIDs = Cache.CreateIndex(a => a.CrossReferences.Select(b => b.AnidbAnimeID).Distinct().WhereNotDefault());
    }

    public IReadOnlyList<StoredReleaseInfo> GetByEd2k(string ed2k)
        => !string.IsNullOrWhiteSpace(ed2k)
            ? ReadLock(() => _ed2k!.GetMultiple(ed2k))
            : [];

    public StoredReleaseInfo? GetByEd2kAndFileSize(string ed2k, long fileSize)
        => GetByEd2k(ed2k).FirstOrDefault(a => a.FileSize == fileSize);

    public IReadOnlyList<StoredReleaseInfo> GetByGroupAndProviderIDs(string groupId, string source)
        => !string.IsNullOrEmpty(groupId) && !string.IsNullOrEmpty(source)
            ? ReadLock(() => _groupIDs!.GetMultiple((groupId, source)))
            : [];

    public StoredReleaseInfo? GetByReleaseURI(string? releaseUri)
        => !string.IsNullOrEmpty(releaseUri)
            ? ReadLock(() => _releaseURIs!.GetOne(releaseUri))
            : null;

    public IReadOnlyList<StoredReleaseInfo> GetByAnidbEpisodeID(int episodeId)
        => episodeId > 0
            ? ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeId))
            : [];

    public IReadOnlyList<StoredReleaseInfo> GetByAnidbAnimeID(int animeId)
        => animeId > 0
            ? ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId))
            : [];

    public IReadOnlyList<IReleaseGroup> GetReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.Source: "AniDB" } releaseInfo ? releaseInfo.Group : null)
            .WhereNotNull()
            .Distinct()
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();


    public IReadOnlyList<IReleaseGroup> GetUsedReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.Source: "AniDB" } releaseInfo && RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize) is { } ? releaseInfo.Group : null)
            .WhereNotNull()
            .Distinct()
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();

    public IReadOnlyList<IReleaseGroup> GetUnusedReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.Source: "AniDB" } releaseInfo && RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize) is not { } ? releaseInfo.Group : null)
            .WhereNotNull()
            .Distinct()
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();
}
