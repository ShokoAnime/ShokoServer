using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static AniDB_GroupStatusRepository Create()
        {
            return new AniDB_GroupStatusRepository();
        }


        private AniDB_GroupStatusRepository()
        {
            EndDeleteCallback = (obj) =>
            {
                if (obj.AnimeID > 0)
                {
                    logger.Trace("Updating group stats by anime from AniDB_GroupStatusRepository.Delete: {0}", obj.AnimeID);
                    AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
                }
            };
        }

        public AniDB_GroupStatus GetByAnimeIDAndGroupID(int animeid, int groupid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AniDB_GroupStatus cr = session
                    .CreateCriteria(typeof(AniDB_GroupStatus))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("GroupID", groupid))
                    .UniqueResult<AniDB_GroupStatus>();
                return cr;
            }
        }

        public List<AniDB_GroupStatus> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_GroupStatus))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_GroupStatus>();

                return new List<AniDB_GroupStatus>(objs);
            }
        }

        public void DeleteForAnime(int animeid)
        {
            Delete(GetByAnimeID(animeid));
        }
    }
}