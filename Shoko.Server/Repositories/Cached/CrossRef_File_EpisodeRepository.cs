using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_File_EpisodeRepository : BaseCachedRepository<SVR_CrossRef_File_Episode, int>
{
    private readonly ILogger<CrossRef_File_EpisodeRepository> _logger;
    private readonly JobFactory _jobFactory;

    private PocoIndex<int, SVR_CrossRef_File_Episode, string> Hashes;
    private PocoIndex<int, SVR_CrossRef_File_Episode, int> Animes;
    private PocoIndex<int, SVR_CrossRef_File_Episode, int> Episodes;
    private PocoIndex<int, SVR_CrossRef_File_Episode, string> Filenames;

    public override void PopulateIndexes()
    {
        Hashes = new PocoIndex<int, SVR_CrossRef_File_Episode, string>(Cache, a => a.Hash);
        Animes = new PocoIndex<int, SVR_CrossRef_File_Episode, int>(Cache, a => a.AnimeID);
        Episodes = new PocoIndex<int, SVR_CrossRef_File_Episode, int>(Cache, a => a.EpisodeID);
        Filenames = new PocoIndex<int, SVR_CrossRef_File_Episode, string>(Cache, a => a.FileName);
    }

    public override void RegenerateDb()
    {
    }

    public CrossRef_File_EpisodeRepository(DatabaseFactory databaseFactory, JobFactory jobFactory, ILogger<CrossRef_File_EpisodeRepository> logger) : base(databaseFactory)
    {
        _jobFactory = jobFactory;
        _logger = logger;
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
    {
        return entity.CrossRef_File_EpisodeID;
    }

    public List<SVR_CrossRef_File_Episode> GetByHash(string hash)
    {
        return ReadLock(() => Hashes.GetMultiple(hash).OrderBy(a => a.EpisodeOrder).ToList());
    }


    public List<SVR_CrossRef_File_Episode> GetByAnimeID(int animeID)
    {
        return ReadLock(() => Animes.GetMultiple(animeID));
    }


    public List<SVR_CrossRef_File_Episode> GetByFileNameAndSize(string filename, long filesize)
    {
        return ReadLock(() => Filenames.GetMultiple(filename).Where(a => a.FileSize == filesize).ToList());
    }

    /// <summary>
    /// This is the only way to uniquely identify the record other than the IDENTITY
    /// </summary>
    /// <param name="hash"></param>
    /// <param name="episodeID"></param>
    /// <returns></returns>
    public SVR_CrossRef_File_Episode GetByHashAndEpisodeID(string hash, int episodeID)
    {
        return ReadLock(() => Hashes.GetMultiple(hash).FirstOrDefault(a => a.EpisodeID == episodeID));
    }

    public List<SVR_CrossRef_File_Episode> GetByEpisodeID(int episodeID)
    {
        return ReadLock(() => Episodes.GetMultiple(episodeID));
    }
}
