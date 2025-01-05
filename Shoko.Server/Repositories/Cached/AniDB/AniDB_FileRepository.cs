#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_FileRepository(ILogger<AniDB_FileRepository> logger, JobFactory jobFactory, DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_AniDB_File, int>(databaseFactory)
{
    private readonly ILogger<AniDB_FileRepository> _logger = logger;

    private readonly JobFactory _jobFactory = jobFactory;

    private PocoIndex<int, SVR_AniDB_File, string>? _ed2k;

    private PocoIndex<int, SVR_AniDB_File, int>? _fileIDs;

    private PocoIndex<int, SVR_AniDB_File, int>? _internalVersions;

    protected override int SelectKey(SVR_AniDB_File entity)
        => entity.AniDB_FileID;

    public override void PopulateIndexes()
    {
        // Only populated from main thread before these are accessible, so no lock
        _ed2k = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.Hash);
        _fileIDs = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.FileID);
        _internalVersions = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.InternalVersion);
    }

    public override void Save(SVR_AniDB_File obj)
        => Save(obj, true);

    public void Save(SVR_AniDB_File obj, bool updateStats)
    {
        base.Save(obj);
        if (!updateStats)
            return;

        _logger.LogTrace("Updating group stats by file from AniDB_FileRepository.Save: {Hash}", obj.Hash);
        var anime = RepoFactory.CrossRef_File_Episode.GetByEd2k(obj.Hash).Select(a => a.AnimeID).Except([0]).Distinct();
        Task.WhenAll(anime.Select(a => _jobFactory.CreateJob<RefreshAnimeStatsJob>(b => b.AnimeID = a).Process())).GetAwaiter().GetResult();
    }

    public SVR_AniDB_File? GetByHash(string hash)
        => ReadLock(() => _ed2k!.GetOne(hash));

    public IReadOnlyList<SVR_AniDB_File> GetByInternalVersion(int version)
        => ReadLock(() => _internalVersions!.GetMultiple(version));

    public SVR_AniDB_File? GetByEd2kAndFileSize(string ed2k, long fileSize)
        => ReadLock(() => _ed2k!.GetMultiple(ed2k)).FirstOrDefault(a => a.FileSize == fileSize);

    public SVR_AniDB_File? GetByFileID(int fileID)
        => ReadLock(() => _fileIDs!.GetOne(fileID));
}
