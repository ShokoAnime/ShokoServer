using System.Collections.Generic;
using NHibernate.Criterion;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public AniDB_GroupStatusRepository()
        {
            EndDeleteCallback = obj =>
            {
                if (obj.AnimeID > 0)
                {
                    logger.Trace("Updating group stats by anime from AniDB_GroupStatusRepository.Delete: {0}",
                        obj.AnimeID);
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
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