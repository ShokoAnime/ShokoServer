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
            return new AniDB_EpisodeRepository();
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
            return EpisodesIds.GetOne(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Episode cr = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("EpisodeID", id))
                    .UniqueResult<AniDB_Episode>();
                return cr;
            }*/
        }

        public List<AniDB_Episode> GetByAnimeID(int id)
        {
            return Animes.GetMultiple(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }*/
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
        {
            return Animes.GetMultiple(animeid)
                .Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == EpisodeType.Episode)
                .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                    .Add(Restrictions.Eq("EpisodeType", (int) enEpisodeType.Episode))
                    .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }*/
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, EpisodeType epType, int epnumber)
        {
            return Animes.GetMultiple(animeid)
                .Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == epType)
                .ToList();
/*            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                    .Add(Restrictions.Eq("EpisodeType", (int) epType))
                    .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }*/
        }

        public List<AniDB_Episode> GetEpisodesWithMultipleFiles()
        {
            return
                RepoFactory.CrossRef_File_Episode.GetAll()
                    .GroupBy(a => a.EpisodeID)
                    .Where(a => a.Count() > 1)
                    .Select(a => GetByEpisodeID(a.Key))
                    .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps =
                    session.CreateQuery(
                        "FROM AniDB_Episode x WHERE x.EpisodeID IN (Select xref.EpisodeID FROM CrossRef_File_Episode xref GROUP BY xref.EpisodeID HAVING COUNT(xref.EpisodeID) > 1)")
                        .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }*/
        }
    }
}