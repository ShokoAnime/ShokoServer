using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_Anime_TitleRepository
    {
        public void Save(AniDB_Anime_Title obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public AniDB_Anime_Title GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Anime_Title>(id);
            }
        }

        public List<AniDB_Anime_Title> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime_Title))
                    .List<AniDB_Anime_Title>();

                return new List<AniDB_Anime_Title>(objs);
                ;
            }
        }

        public List<AniDB_Anime_Title> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var titles = session
                    .CreateCriteria(typeof(AniDB_Anime_Title))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_Anime_Title>();

                return new List<AniDB_Anime_Title>(titles);
            }
        }

        public List<AniDB_Anime_Title> GetByAnimeID(ISession session, int id)
        {
            var titles = session
                .CreateCriteria(typeof(AniDB_Anime_Title))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Anime_Title>();

            return new List<AniDB_Anime_Title>(titles);
        }

        public List<AniDB_Anime_Title> GetByAnimeIDLanguageTypeValue(int animeID, string language, string titleType,
            string titleValue)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var titles = session
                    .CreateCriteria(typeof(AniDB_Anime_Title))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .Add(Restrictions.Eq("TitleType", titleType))
                    .Add(Restrictions.Eq("Language", language))
                    .Add(Restrictions.Eq("Title", titleValue))
                    .List<AniDB_Anime_Title>();

                return new List<AniDB_Anime_Title>(titles);
            }
        }

        /// <summary>
        ///     Gets all the anime titles, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Anime_Title> GetAllForLocalSeries()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var titles =
                    session.CreateQuery(
                        "FROM AniDB_Anime_Title aat WHERE aat.AnimeID IN (Select aser.AniDB_ID From AnimeSeries aser)")
                        .List<AniDB_Anime_Title>();

                return new List<AniDB_Anime_Title>(titles);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    var cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}