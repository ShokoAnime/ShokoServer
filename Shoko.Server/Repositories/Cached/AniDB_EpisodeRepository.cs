using System;
using System.Collections.Generic;
using System.Linq;
using AniDBAPI;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories
{
    public class AniDB_EpisodeRepository : BaseCachedRepository<AniDB_Episode, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        private PocoIndex<int, AniDB_Episode, int> EpisodesIds;
        private PocoIndex<int, AniDB_Episode, int> Animes;

        public override void PopulateIndexes()
        {
            EpisodesIds = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.EpisodeID);
            Animes = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.AnimeID);
        }

        private AniDB_EpisodeRepository()
        {
        }

        public static AniDB_EpisodeRepository Create()
        {
            var repo = new AniDB_EpisodeRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

        protected override int SelectKey(AniDB_Episode entity)
        {
            return entity.AniDB_EpisodeID;
        }

        public override void RegenerateDb()
        {
        }


        public AniDB_Episode GetByEpisodeID(int id)
        {
            lock (Cache)
            {
                return EpisodesIds.GetOne(id);
            }
        }

        public List<AniDB_Episode> GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return Animes.GetMultiple(id);
            }
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
        {
            lock (Cache)
            {
                return Animes.GetMultiple(animeid)
                    .Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == EpisodeType.Episode)
                    .ToList();
            }
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, EpisodeType epType, int epnumber)
        {
            lock (Cache)
            {
                return Animes.GetMultiple(animeid)
                    .Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == epType)
                    .ToList();
            }
        }

        public List<AniDB_Episode> GetEpisodesWithMultipleFiles()
        {
            return
                RepoFactory.CrossRef_File_Episode.GetAll()
                    .GroupBy(a => a.EpisodeID)
                    .Where(a => a.Count() > 1)
                    .Select(a => GetByEpisodeID(a.Key))
                    .ToList();
        }
    }
}
