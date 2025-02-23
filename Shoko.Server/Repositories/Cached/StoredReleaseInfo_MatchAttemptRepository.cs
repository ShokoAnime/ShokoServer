using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Release;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class StoredReleaseInfo_MatchAttemptRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<StoredReleaseInfo_MatchAttempt, int>(databaseFactory)
{
    private PocoIndex<int, StoredReleaseInfo_MatchAttempt, string>? _ed2k;

    private PocoIndex<int, StoredReleaseInfo_MatchAttempt, string>? _sourceProviderIDs;

    private PocoIndex<int, StoredReleaseInfo_MatchAttempt, string?>? _resultProviderIDs;

    protected override int SelectKey(StoredReleaseInfo_MatchAttempt entity)
        => entity.StoredReleaseInfo_MatchAttemptID;

    public override void PopulateIndexes()
    {
        _ed2k = Cache.CreateIndex(a => a.ED2K);
        _sourceProviderIDs = Cache.CreateIndex(a => a.AttemptedProviderIDs);
        _resultProviderIDs = Cache.CreateIndex(a => a.ProviderID);
    }

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetByEd2k(string ed2k)
        => !string.IsNullOrWhiteSpace(ed2k)
            ? ReadLock(() => _ed2k!.GetMultiple(ed2k))
            : [];

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetByEd2kAndFileSize(string ed2k, long fileSize)
        => GetByEd2k(ed2k).Where(a => a.FileSize == fileSize).ToList();

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetBySourceProviderIDs(string providerId)
        => !string.IsNullOrEmpty(providerId)
            ? ReadLock(() => _sourceProviderIDs!.GetMultiple(providerId))
            : [];

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetByResultProviderIDs(string? providerId)
        => ReadLock(() => _resultProviderIDs!.GetMultiple(providerId));
}
