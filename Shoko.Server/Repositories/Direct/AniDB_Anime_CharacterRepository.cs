using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_Anime_CharacterRepository : BaseDirectRepository<SVR_AniDB_Anime_Character, int>
    {
        private AniDB_Anime_CharacterRepository()
        {
            
        }

        public static AniDB_Anime_CharacterRepository Create()
        {
            return new AniDB_Anime_CharacterRepository();
        }
        public List<SVR_AniDB_Anime_Character> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cats = session
                    .CreateCriteria(typeof(SVR_AniDB_Anime_Character))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<SVR_AniDB_Anime_Character>();

                return new List<SVR_AniDB_Anime_Character>(cats);
            }
        }

        public List<SVR_AniDB_Anime_Character> GetByAnimeID(ISessionWrapper session, int id)
        {
            var cats = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Character))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<SVR_AniDB_Anime_Character>();

            return new List<SVR_AniDB_Anime_Character>(cats);
        }

        public List<SVR_AniDB_Anime_Character> GetByCharID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cats = session
                    .CreateCriteria(typeof(SVR_AniDB_Anime_Character))
                    .Add(Restrictions.Eq("CharID", id))
                    .List<SVR_AniDB_Anime_Character>();

                return new List<SVR_AniDB_Anime_Character>(cats);
            }
        }

        public SVR_AniDB_Anime_Character GetByAnimeIDAndCharID(int animeid, int charid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_AniDB_Anime_Character cr = session
                    .CreateCriteria(typeof(SVR_AniDB_Anime_Character))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("CharID", charid))
                    .UniqueResult<SVR_AniDB_Anime_Character>();

                return cr;
            }
        }

    }
}