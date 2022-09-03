using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;

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

        protected override int SelectKey(AniDB_Episode entity)
        {
            return entity.AniDB_EpisodeID;
        }

        public override void RegenerateDb()
        {
        }


        public AniDB_Episode GetByEpisodeID(int id)
        {
            lock (GlobalLock)
            {
                return EpisodesIds.GetOne(id);
            }
        }

        public List<AniDB_Episode> GetByAnimeID(int id)
        {
            lock (GlobalLock)
            {
                return Animes.GetMultiple(id);
            }
        }

        public List<AniDB_Episode> GetForDate(DateTime startDate, DateTime endDate)
        {
            lock (GlobalLock)
            {
                return Cache.Values.Where(a => {
                    var date = a.GetAirDateAsDate();
                    return date.HasValue && date.Value >= startDate && date.Value <= endDate;
                }).ToList();
            }
        }


        public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
        {
            lock (GlobalLock)
            {
                return Animes.GetMultiple(animeid)
                    .Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == EpisodeType.Episode)
                    .ToList();
            }
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, EpisodeType epType, int epnumber)
        {
            lock (GlobalLock)
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
