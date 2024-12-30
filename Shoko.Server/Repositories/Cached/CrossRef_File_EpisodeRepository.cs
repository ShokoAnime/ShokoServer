using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_File_EpisodeRepository : BaseCachedRepository<SVR_CrossRef_File_Episode, int>
{
    private readonly ILogger<CrossRef_File_EpisodeRepository> _logger;

    private readonly JobFactory _jobFactory;

    private PocoIndex<int, SVR_CrossRef_File_Episode, string>? _ed2k;

    private PocoIndex<int, SVR_CrossRef_File_Episode, int>? _anidbAnimeIDs;

    private PocoIndex<int, SVR_CrossRef_File_Episode, int>? _anidbEpisodeIDs;

    private PocoIndex<int, SVR_CrossRef_File_Episode, (string FileName, long FileSize)>? _fileNames;

    public CrossRef_File_EpisodeRepository(ILogger<CrossRef_File_EpisodeRepository> logger, JobFactory jobFactory, DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        _logger = logger;
        _jobFactory = jobFactory;
        EndSaveCallback = obj =>
        {
            var job = _jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = obj.AnimeID);
            job.Process().GetAwaiter().GetResult();
        };
        EndDeleteCallback = obj =>
        {
            if (obj is not { AnimeID: > 0 }) return;

            _logger.LogTrace("Updating group stats by anime from CrossRef_File_EpisodeRepository.Delete: {AnimeID}", obj.AnimeID);
            var job = _jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = obj.AnimeID);
            job.Process().GetAwaiter().GetResult();
        };
    }

    protected override int SelectKey(SVR_CrossRef_File_Episode entity)
        => entity.CrossRef_File_EpisodeID;

    public override void PopulateIndexes()
    {
        _ed2k = new PocoIndex<int, SVR_CrossRef_File_Episode, string>(Cache, a => a.Hash);
        _anidbAnimeIDs = new PocoIndex<int, SVR_CrossRef_File_Episode, int>(Cache, a => a.AnimeID);
        _anidbEpisodeIDs = new PocoIndex<int, SVR_CrossRef_File_Episode, int>(Cache, a => a.EpisodeID);
        _fileNames = new PocoIndex<int, SVR_CrossRef_File_Episode, (string FileName, long FileSize)>(Cache, a => (a.FileName, a.FileSize));
    }

    public IReadOnlyList<SVR_CrossRef_File_Episode> GetByEd2k(string ed2k)
        => ReadLock(() => _ed2k!.GetMultiple(ed2k).OrderBy(a => a.EpisodeOrder).ToList());

    public IReadOnlyList<SVR_CrossRef_File_Episode> GetByAnimeID(int animeID)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeID));

    public IReadOnlyList<SVR_CrossRef_File_Episode> GetByFileNameAndSize(string fileName, long fileSize)
        => ReadLock(() => _fileNames!.GetMultiple((fileName, fileSize)));

    public IReadOnlyList<SVR_CrossRef_File_Episode> GetByEpisodeID(int episodeID)
        => ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeID));
}
