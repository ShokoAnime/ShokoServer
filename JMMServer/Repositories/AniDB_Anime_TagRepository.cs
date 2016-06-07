using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_Anime_TagRepository
    {
        public void Save(AniDB_Anime_Tag obj)
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

        public AniDB_Anime_Tag GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Anime_Tag>(id);
            }
        }

        public List<AniDB_Anime_Tag> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime_Tag))
                    .List<AniDB_Anime_Tag>();

                return new List<AniDB_Anime_Tag>(objs);
                ;
            }
        }

        public AniDB_Anime_Tag GetByAnimeIDAndTagID(int animeid, int tagid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var cr = session
                    .CreateCriteria(typeof(AniDB_Anime_Tag))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("TagID", tagid))
                    .UniqueResult<AniDB_Anime_Tag>();
                return cr;
            }
        }

        public AniDB_Anime_Tag GetByAnimeIDAndTagID(ISession session, int animeid, int tagid)
        {
            var cr = session
                .CreateCriteria(typeof(AniDB_Anime_Tag))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("TagID", tagid))
                .UniqueResult<AniDB_Anime_Tag>();
            return cr;
        }

        public List<AniDB_Anime_Tag> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags = session
                    .CreateCriteria(typeof(AniDB_Anime_Tag))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_Anime_Tag>();

                return new List<AniDB_Anime_Tag>(tags);
            }
        }

        public List<AniDB_Anime_Tag> GetByAnimeID(ISession session, int id)
        {
            var tags = session
                .CreateCriteria(typeof(AniDB_Anime_Tag))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Anime_Tag>();

            return new List<AniDB_Anime_Tag>(tags);
        }

        /// <summary>
        ///     Gets all the anime tags, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Anime_Tag> GetAllForLocalSeries()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags =
                    session.CreateQuery(
                        "FROM AniDB_Anime_Tag tag WHERE tag.AnimeID in (Select aser.AniDB_ID From AnimeSeries aser)")
                        .List<AniDB_Anime_Tag>();

                return new List<AniDB_Anime_Tag>(tags);
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