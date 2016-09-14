using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using System.Linq;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AniDB_TagRepository
    {
        private static PocoCache<int, AniDB_Tag> Cache;
        private static PocoIndex<int, AniDB_Tag, int> Tags;

        public static void InitCache()
        {
            string t = "AniDB_Tag";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            AniDB_TagRepository repo = new AniDB_TagRepository();
            Cache = new PocoCache<int, AniDB_Tag>(repo.InternalGetAll(), a => a.AniDB_TagID);
            Tags = new PocoIndex<int, AniDB_Tag, int>(Cache, a => a.TagID);
        }
        internal List<AniDB_Tag> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Tag))
                    .List<AniDB_Tag>();

                return new List<AniDB_Tag>(objs);
            }
        }
        public void Save(AniDB_Tag obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                    Cache.Update(obj);
                }
            }
        }
        public void Save(IEnumerable<AniDB_Tag> objs)
        {
            if (!objs.Any())
                return;

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    foreach(AniDB_Tag obj in objs)
                        session.SaveOrUpdate(obj);
                    transaction.Commit();
                    foreach (AniDB_Tag obj in objs)
                        Cache.Update(obj);
                }
            }
        }
        public AniDB_Tag GetByID(int id)
        {
            return Cache.Get(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Tag>(id);
            }*/
        }

        public List<AniDB_Tag> GetAll()
        {
            return Cache.Values.ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Tag))
                    .List<AniDB_Tag>();

                return new List<AniDB_Tag>(objs);
                ;
            }*/
        }


        public List<AniDB_Tag> GetByAnimeID(int animeID)
        {
            return new AniDB_Anime_TagRepository().GetByAnimeID(animeID).Select(a => GetByTagID(a.TagID)).Where(a=>a!=null).ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags =
                    session.CreateQuery(
                        "Select tag FROM AniDB_Tag as tag, AniDB_Anime_Tag as xref WHERE tag.TagID = xref.TagID AND xref.AnimeID= :animeID")
                        .SetParameter("animeID", animeID)
                        .List<AniDB_Tag>();

                return new List<AniDB_Tag>(tags);
            }*/
        }



        public ILookup<int, AniDB_Tag> GetByAnimeIDs(ISessionWrapper session, int[] ids)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Length == 0)
            {
                return EmptyLookup<int, AniDB_Tag>.Instance;
            }

            var tags = session
                .CreateQuery("Select xref.AnimeID, tag FROM AniDB_Tag as tag, AniDB_Anime_Tag as xref WHERE tag.TagID = xref.TagID AND xref.AnimeID IN (:animeIDs)")
                .SetParameterList("animeIDs", ids)
                .List<object[]>()
                .ToLookup(t => (int)t[0], t => (AniDB_Tag)t[1]);

            return tags;
        }


        public AniDB_Tag GetByTagID(int id)
        {
            return Tags.GetOne(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Tag cr = session
                    .CreateCriteria(typeof(AniDB_Tag))
                    .Add(Restrictions.Eq("TagID", id))
                    .UniqueResult<AniDB_Tag>();

                return cr;
            }*/
        }



        /// <summary>
        /// Gets all the tags, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Tag> GetAllForLocalSeries()
        {
            AniDB_Anime_TagRepository pp=new AniDB_Anime_TagRepository();
            return new AnimeSeriesRepository().GetAll().SelectMany(a=>pp.GetByAnimeID(a.AniDB_ID)).Where(a=>a!=null).Select(a=>GetByTagID(a.TagID)).Distinct().ToList();
            /*

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags =
                    session.CreateQuery(
                        "FROM AniDB_Tag tag WHERE tag.TagID in (SELECT aat.TagID FROM AniDB_Anime_Tag aat, AnimeSeries aser WHERE aat.AnimeID = aser.AniDB_ID)")
                        .List<AniDB_Tag>();

                return new List<AniDB_Tag>(tags);
            }*/
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
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
        public void Delete(IEnumerable<AniDB_Tag> tags)
        {
            if (!tags.Any())
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    foreach (AniDB_Tag cr in tags)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                    }
                    transaction.Commit();
                }
            }
        }
    }
}