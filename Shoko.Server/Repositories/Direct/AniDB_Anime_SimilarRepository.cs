using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_Anime_SimilarRepository : BaseDirectRepository<SVR_AniDB_Anime_Similar, int>
    {

        private AniDB_Anime_SimilarRepository()
        {
            
        }

        public static AniDB_Anime_SimilarRepository Create()
        {
            return new AniDB_Anime_SimilarRepository();
        }
        public SVR_AniDB_Anime_Similar GetByAnimeIDAndSimilarID(int animeid, int similaranimeid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_AniDB_Anime_Similar cr = session
                    .CreateCriteria(typeof(SVR_AniDB_Anime_Similar))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("SimilarAnimeID", similaranimeid))
                    .UniqueResult<SVR_AniDB_Anime_Similar>();
                return cr;
            }
        }

        public SVR_AniDB_Anime_Similar GetByAnimeIDAndSimilarID(ISession session, int animeid, int similaranimeid)
        {
            SVR_AniDB_Anime_Similar cr = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Similar))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("SimilarAnimeID", similaranimeid))
                .UniqueResult<SVR_AniDB_Anime_Similar>();
            return cr;
        }

        public List<SVR_AniDB_Anime_Similar> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public List<SVR_AniDB_Anime_Similar> GetByAnimeID(ISession session, int id)
        {
            var cats = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Similar))
                .Add(Restrictions.Eq("AnimeID", id))
                .AddOrder(Order.Desc("Approval"))
                .List<SVR_AniDB_Anime_Similar>();

            return new List<SVR_AniDB_Anime_Similar>(cats);
        }

        
    }
}