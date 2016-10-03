using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_Anime_ReviewRepository : BaseDirectRepository<AniDB_Anime_Review, int>
    {

        private AniDB_Anime_ReviewRepository()
        {
            
        }

        public static AniDB_Anime_ReviewRepository Create()
        {
            return new AniDB_Anime_ReviewRepository();
        }
        public AniDB_Anime_Review GetByAnimeIDAndReviewID(int animeid, int reviewid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AniDB_Anime_Review cr = session
                    .CreateCriteria(typeof(AniDB_Anime_Review))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("ReviewID", reviewid))
                    .UniqueResult<AniDB_Anime_Review>();
                return cr;
            }
        }

        public List<AniDB_Anime_Review> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cats = session
                    .CreateCriteria(typeof(AniDB_Anime_Review))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_Anime_Review>();

                return new List<AniDB_Anime_Review>(cats);
            }
        }

    }
}