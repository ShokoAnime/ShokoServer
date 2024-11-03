using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_FileRepository : BaseCachedRepository<SVR_AniDB_File, int>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly JobFactory _jobFactory;

    private PocoIndex<int, SVR_AniDB_File, string> Hashes;
    private PocoIndex<int, SVR_AniDB_File, int> FileIds;
    private PocoIndex<int, SVR_AniDB_File, int> InternalVersions;

    protected override int SelectKey(SVR_AniDB_File entity)
    {
        return entity.AniDB_FileID;
    }

    public override void PopulateIndexes()
    {
        // Only populated from main thread before these are accessible, so no lock
        Hashes = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.Hash);
        FileIds = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.FileID);
        InternalVersions = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.InternalVersion);
    }

    public override void RegenerateDb()
    {
    }

    public override void Save(SVR_AniDB_File obj)
    {
        Save(obj, true);
    }

    public void Save(SVR_AniDB_File obj, bool updateStats)
    {
        base.Save(obj);
        if (!updateStats)
        {
            return;
        }

        Logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {Hash}", obj.Hash);
        var anime = RepoFactory.CrossRef_File_Episode.GetByHash(obj.Hash).Select(a => a.AnimeID).Except([0]).Distinct();
        Task.WhenAll(anime.Select(a => _jobFactory.CreateJob<RefreshAnimeStatsJob>(b => b.AnimeID = a).Process())).GetAwaiter().GetResult();
    }


    public SVR_AniDB_File GetByHash(string hash)
    {
        return ReadLock(() => Hashes.GetOne(hash));
    }

    public List<SVR_AniDB_File> GetByInternalVersion(int version)
    {
        return ReadLock(() => InternalVersions.GetMultiple(version));
    }

    public SVR_AniDB_File GetByHashAndFileSize(string hash, long fsize)
    {
        var list = ReadLock(() => Hashes.GetMultiple(hash));
        return list.Count == 1 ? list.FirstOrDefault() : list.FirstOrDefault(a => a.FileSize == fsize);
    }

    public SVR_AniDB_File GetByFileID(int fileID)
    {
        return ReadLock(() => FileIds.GetOne(fileID));
    }

    public AniDB_FileRepository(DatabaseFactory databaseFactory, JobFactory jobFactory) : base(databaseFactory)
    {
        _jobFactory = jobFactory;
    }
}
