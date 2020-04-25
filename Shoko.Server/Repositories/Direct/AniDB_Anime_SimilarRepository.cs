using System.Collections.Generic;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_Anime_SimilarRepository : BaseDirectRepository<AniDB_Anime_Similar, int>
    {
        private AniDB_Anime_SimilarRepository()
        {
        }

        public static AniDB_Anime_SimilarRepository Create()
        {
            return new AniDB_Anime_SimilarRepository();
        }

        public AniDB_Anime_Similar GetByAnimeIDAndSimilarID(int animeid, int similaranimeid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AniDB_Anime_Similar cr = session
                    .CreateCriteria(typeof(AniDB_Anime_Similar))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("SimilarAnimeID", similaranimeid))
                    .UniqueResult<AniDB_Anime_Similar>();
                return cr;
            }
        }

        public AniDB_Anime_Similar GetByAnimeIDAndSimilarID(ISession session, int animeid, int similaranimeid)
        {
            AniDB_Anime_Similar cr = session
                .CreateCriteria(typeof(AniDB_Anime_Similar))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("SimilarAnimeID", similaranimeid))
                .UniqueResult<AniDB_Anime_Similar>();
            return cr;
        }

        public List<AniDB_Anime_Similar> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public List<AniDB_Anime_Similar> GetByAnimeID(ISession session, int id)
        {
            var cats = session
                .CreateCriteria(typeof(AniDB_Anime_Similar))
                .Add(Restrictions.Eq("AnimeID", id))
                .AddOrder(Order.Desc("Approval"))
                .List<AniDB_Anime_Similar>();

            return new List<AniDB_Anime_Similar>(cats);
        }
    }
}