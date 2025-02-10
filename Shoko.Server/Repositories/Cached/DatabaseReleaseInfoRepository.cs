using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Databases;
using Shoko.Server.Models.Release;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class DatabaseReleaseInfoRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<DatabaseReleaseInfo, int>(databaseFactory)
{
    private PocoIndex<int, DatabaseReleaseInfo, string>? _ed2k;
    private PocoIndex<int, DatabaseReleaseInfo, (string groupId, string providerId)>? _groupIDs;
    private PocoIndex<int, DatabaseReleaseInfo, string?>? _releaseURIs;
    private PocoIndex<int, DatabaseReleaseInfo, int>? _anidbEpisodeIDs;
    private PocoIndex<int, DatabaseReleaseInfo, int>? _anidbAnimeIDs;

    protected override int SelectKey(DatabaseReleaseInfo entity)
        => entity.DatabaseReleaseInfoID;

    public override void PopulateIndexes()
    {
        _ed2k = Cache.CreateIndex(a => a.ED2K);
        _groupIDs = Cache.CreateIndex(a => (a.GroupID ?? string.Empty, a.GroupProviderID ?? string.Empty));
        _releaseURIs = Cache.CreateIndex(a => a.ReleaseURI);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.CrossReferences.Select(b => b.AnidbEpisodeID));
        _anidbAnimeIDs = Cache.CreateIndex(a => a.CrossReferences.Select(b => b.AnidbAnimeID).Distinct().WhereNotDefault());
    }

    public IReadOnlyList<DatabaseReleaseInfo> GetByEd2k(string ed2k)
        => !string.IsNullOrWhiteSpace(ed2k)
            ? ReadLock(() => _ed2k!.GetMultiple(ed2k))
            : [];

    public DatabaseReleaseInfo? GetByEd2kAndFileSize(string ed2k, long fileSize)
        => GetByEd2k(ed2k).FirstOrDefault(a => a.FileSize == fileSize);

    public IReadOnlyList<DatabaseReleaseInfo> GetByGroupAndProviderIDs(string groupId, string providerId)
        => !string.IsNullOrEmpty(groupId) && !string.IsNullOrEmpty(providerId)
            ? ReadLock(() => _groupIDs!.GetMultiple((groupId, providerId)))
            : [];

    public DatabaseReleaseInfo? GetByReleaseURI(string? releaseUri)
        => !string.IsNullOrEmpty(releaseUri)
            ? ReadLock(() => _releaseURIs!.GetOne(releaseUri))
            : null;

    public IReadOnlyList<DatabaseReleaseInfo> GetByAnidbEpisodeID(int episodeId)
        => episodeId > 0
            ? ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeId))
            : [];

    public IReadOnlyList<DatabaseReleaseInfo> GetByAnidbAnimeID(int animeId)
        => animeId > 0
            ? ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId))
            : [];

    public IReadOnlyList<IReleaseGroup> GetReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.ProviderID: "AniDB" } releaseInfo ? releaseInfo.Group : null)
            .WhereNotNull()
            .Distinct()
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();


    public IReadOnlyList<IReleaseGroup> GetUsedReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.ProviderID: "AniDB" } releaseInfo && RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize) is { } ? releaseInfo.Group : null)
            .WhereNotNull()
            .Distinct()
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();

    public IReadOnlyList<IReleaseGroup> GetUnusedReleaseGroups()
        => GetAll()
            .Select(a => a is IReleaseInfo { Group.ProviderID: "AniDB" } releaseInfo && RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize) is not { } ? releaseInfo.Group : null)
            .WhereNotNull()
            .Distinct()
            .OrderBy(g => g.Name)
            .ThenBy(g => g.ShortName)
            .ThenBy(g => g.ID)
            .ToList();
}
