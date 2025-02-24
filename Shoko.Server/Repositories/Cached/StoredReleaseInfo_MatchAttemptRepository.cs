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

    private PocoIndex<int, StoredReleaseInfo_MatchAttempt, string>? _sourceProviderNames;

    private PocoIndex<int, StoredReleaseInfo_MatchAttempt, string?>? _resultProviderNames;

    protected override int SelectKey(StoredReleaseInfo_MatchAttempt entity)
        => entity.StoredReleaseInfo_MatchAttemptID;

    public override void PopulateIndexes()
    {
        _ed2k = Cache.CreateIndex(a => a.ED2K);
        _sourceProviderNames = Cache.CreateIndex(a => a.AttemptedProviderNames);
        _resultProviderNames = Cache.CreateIndex(a => a.ProviderName);
    }

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetByEd2k(string ed2k)
        => !string.IsNullOrWhiteSpace(ed2k)
            ? ReadLock(() => _ed2k!.GetMultiple(ed2k))
            : [];

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetByEd2kAndFileSize(string ed2k, long fileSize)
        => GetByEd2k(ed2k).Where(a => a.FileSize == fileSize).ToList();

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetBySourceProviderNames(string providerName)
        => !string.IsNullOrEmpty(providerName)
            ? ReadLock(() => _sourceProviderNames!.GetMultiple(providerName))
            : [];

    public IReadOnlyList<StoredReleaseInfo_MatchAttempt> GetByResultProviderNames(string? providerName)
        => ReadLock(() => _resultProviderNames!.GetMultiple(providerName));
}
