using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_Anime_RelationRepository : BaseDirectRepository<SVR_AniDB_Anime_Relation, int>
    {
        private AniDB_Anime_RelationRepository()
        {
            
        }

        public static AniDB_Anime_RelationRepository Create()
        {
            return new AniDB_Anime_RelationRepository();
        }
        public SVR_AniDB_Anime_Relation GetByAnimeIDAndRelationID(int animeid, int relatedanimeid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeIDAndRelationID(session, animeid, relatedanimeid);
            }
        }

        public SVR_AniDB_Anime_Relation GetByAnimeIDAndRelationID(ISession session, int animeid, int relatedanimeid)
        {
            SVR_AniDB_Anime_Relation cr = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Relation))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("RelatedAnimeID", relatedanimeid))
                .UniqueResult<SVR_AniDB_Anime_Relation>();
            return cr;
        }

        public List<SVR_AniDB_Anime_Relation> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), id);
            }
        }

        public List<SVR_AniDB_Anime_Relation> GetByAnimeID(ISessionWrapper session, int id)
        {
            var cats = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Relation))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<SVR_AniDB_Anime_Relation>();

            return new List<SVR_AniDB_Anime_Relation>(cats);
        }
    }
}