using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_TagRepository
    {
        public void Save(AniDB_Tag obj)
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

        public AniDB_Tag GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Tag>(id);
            }
        }

        public List<AniDB_Tag> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Tag))
                    .List<AniDB_Tag>();

                return new List<AniDB_Tag>(objs);
                ;
            }
        }

        public List<AniDB_Tag> GetAll(ISession session)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Tag))
                .List<AniDB_Tag>();

            return new List<AniDB_Tag>(objs);
            ;
        }

        public List<AniDB_Tag> GetByAnimeID(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags =
                    session.CreateQuery(
                        "Select tag FROM AniDB_Tag as tag, AniDB_Anime_Tag as xref WHERE tag.TagID = xref.TagID AND xref.AnimeID= :animeID")
                        .SetParameter("animeID", animeID)
                        .List<AniDB_Tag>();

                return new List<AniDB_Tag>(tags);
            }
        }

        public List<AniDB_Tag> GetByAnimeID(ISession session, int animeID)
        {
            var tags =
                session.CreateQuery(
                    "Select tag FROM AniDB_Tag as tag, AniDB_Anime_Tag as xref WHERE tag.TagID = xref.TagID AND xref.AnimeID= :animeID")
                    .SetParameter("animeID", animeID)
                    .List<AniDB_Tag>();

            return new List<AniDB_Tag>(tags);
        }

        public AniDB_Tag GetByTagID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Tag cr = session
                    .CreateCriteria(typeof(AniDB_Tag))
                    .Add(Restrictions.Eq("TagID", id))
                    .UniqueResult<AniDB_Tag>();

                return cr;
            }
        }

        public AniDB_Tag GetByTagID(int id, ISession session)
        {
            AniDB_Tag cr = session
                .CreateCriteria(typeof(AniDB_Tag))
                .Add(Restrictions.Eq("TagID", id))
                .UniqueResult<AniDB_Tag>();

            return cr;
        }

        /// <summary>
        /// Gets all the tags, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Tag> GetAllForLocalSeries()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags =
                    session.CreateQuery(
                        "FROM AniDB_Tag tag WHERE tag.TagID in (SELECT aat.TagID FROM AniDB_Anime_Tag aat, AnimeSeries aser WHERE aat.AnimeID = aser.AniDB_ID)")
                        .List<AniDB_Tag>();

                return new List<AniDB_Tag>(tags);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Tag cr = GetByID(id);
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