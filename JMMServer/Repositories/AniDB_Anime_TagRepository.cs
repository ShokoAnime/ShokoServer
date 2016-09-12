using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AniDB_Anime_TagRepository
    {
        private static PocoCache<int, AniDB_Anime_Tag> Cache;
        private static PocoIndex<int, AniDB_Anime_Tag, int> Animes;
        public static void InitCache()
        {
            string t = "AniDB_Anime_Tag";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            AniDB_Anime_TagRepository repo = new AniDB_Anime_TagRepository();
            Cache = new PocoCache<int, AniDB_Anime_Tag>(repo.InternalGetAll(), a => a.AniDB_Anime_TagID);
            Animes = new PocoIndex<int, AniDB_Anime_Tag, int>(Cache, a => a.AnimeID);
        }

        internal List<AniDB_Anime_Tag> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime_Tag))
                    .List<AniDB_Anime_Tag>();

                return new List<AniDB_Anime_Tag>(objs);
            }
        }
        public void Save(AniDB_Anime_Tag obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    Cache.Update(obj);
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public AniDB_Anime_Tag GetByID(int id)
        {
            return Cache.Get(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Anime_Tag>(id);
            }*/
        }

        public List<AniDB_Anime_Tag> GetAll()
        {
            return Cache.Values.ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime_Tag))
                    .List<AniDB_Anime_Tag>();

                return new List<AniDB_Anime_Tag>(objs);
                ;
            }*/
        }

        public AniDB_Anime_Tag GetByAnimeIDAndTagID(int animeid, int tagid)
        {
            return Animes.GetMultiple(animeid).FirstOrDefault(a => a.TagID == tagid);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Anime_Tag cr = session
                    .CreateCriteria(typeof(AniDB_Anime_Tag))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("TagID", tagid))
                    .UniqueResult<AniDB_Anime_Tag>();
                return cr;
            }*/
        }



        public List<AniDB_Anime_Tag> GetByAnimeID(int id)
        {
            return Animes.GetMultiple(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags = session
                    .CreateCriteria(typeof(AniDB_Anime_Tag))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_Anime_Tag>();

                return new List<AniDB_Anime_Tag>(tags);
            }*/
        }



        public ILookup<int, AniDB_Anime_Tag> GetByAnimeIDs(ISessionWrapper session, ICollection<int> ids)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Count == 0)
            {
                return EmptyLookup<int, AniDB_Anime_Tag>.Instance;
            }

            var tags = session.CreateCriteria<AniDB_Anime_Tag>()
                .Add(Restrictions.InG(nameof(AniDB_Anime_Tag.AnimeID), ids))
                .List<AniDB_Anime_Tag>()
                .ToLookup(t => t.AnimeID);

            return tags;
        }

        /// <summary>
        /// Gets all the anime tags, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Anime_Tag> GetAllForLocalSeries()
        {
            return new AnimeSeriesRepository().GetAll()
                .SelectMany(a => GetByAnimeID(a.AniDB_ID))
                .Where(a => a != null)
                .Distinct()
                .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags =
                    session.CreateQuery(
                        "FROM AniDB_Anime_Tag tag WHERE tag.AnimeID in (Select aser.AniDB_ID From AnimeSeries aser)")
                        .List<AniDB_Anime_Tag>();

                return new List<AniDB_Anime_Tag>(tags);
            }*/
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Anime_Tag cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}