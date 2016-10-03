using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_Anime_CharacterRepository : BaseDirectRepository<AniDB_Anime_Character, int>
    {
        private AniDB_Anime_CharacterRepository()
        {
            
        }

        public static AniDB_Anime_CharacterRepository Create()
        {
            return new AniDB_Anime_CharacterRepository();
        }
        public List<AniDB_Anime_Character> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cats = session
                    .CreateCriteria(typeof(AniDB_Anime_Character))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_Anime_Character>();

                return new List<AniDB_Anime_Character>(cats);
            }
        }

        public List<AniDB_Anime_Character> GetByAnimeID(ISessionWrapper session, int id)
        {
            var cats = session
                .CreateCriteria(typeof(AniDB_Anime_Character))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Anime_Character>();

            return new List<AniDB_Anime_Character>(cats);
        }

        public List<AniDB_Anime_Character> GetByCharID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cats = session
                    .CreateCriteria(typeof(AniDB_Anime_Character))
                    .Add(Restrictions.Eq("CharID", id))
                    .List<AniDB_Anime_Character>();

                return new List<AniDB_Anime_Character>(cats);
            }
        }

        public AniDB_Anime_Character GetByAnimeIDAndCharID(int animeid, int charid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AniDB_Anime_Character cr = session
                    .CreateCriteria(typeof(AniDB_Anime_Character))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("CharID", charid))
                    .UniqueResult<AniDB_Anime_Character>();

                return cr;
            }
        }

    }
}