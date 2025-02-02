using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models.Release;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class DatabaseReleaseInfoRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<DatabaseReleaseInfo, int>(databaseFactory)
{
    private PocoIndex<int, DatabaseReleaseInfo, string>? _ed2k;

    protected override int SelectKey(DatabaseReleaseInfo entity)
        => entity.DatabaseReleaseInfoID;

    public override void PopulateIndexes()
    {
        _ed2k = new PocoIndex<int, DatabaseReleaseInfo, string>(Cache, a => a.ED2K);
    }

    public IReadOnlyList<DatabaseReleaseInfo> GetByEd2k(string ed2k)
        => !string.IsNullOrWhiteSpace(ed2k)
            ? ReadLock(() => _ed2k!.GetMultiple(ed2k))
            : [];

    public DatabaseReleaseInfo? GetByEd2kAndFileSize(string ed2k, long fileSize)
        => GetByEd2k(ed2k).FirstOrDefault(a => a.FileSize == fileSize);
}
