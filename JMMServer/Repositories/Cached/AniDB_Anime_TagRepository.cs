using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AniDB_Anime_TagRepository : BaseCachedRepository<SVR_AniDB_Anime_Tag, int>
    {
        private PocoIndex<int, SVR_AniDB_Anime_Tag, int> Animes;

        private AniDB_Anime_TagRepository() { }
        public override void RegenerateDb() { }

        public static AniDB_Anime_TagRepository Create()
        {
            return new AniDB_Anime_TagRepository();
        }

        protected override int SelectKey(SVR_AniDB_Anime_Tag entity)
        {
            return entity.AniDB_Anime_TagID;
        }

        public override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, SVR_AniDB_Anime_Tag, int>(Cache, a => a.AnimeID);
        }

        public SVR_AniDB_Anime_Tag GetByAnimeIDAndTagID(int animeid, int tagid)
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



        public List<SVR_AniDB_Anime_Tag> GetByAnimeID(int id)
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



        public ILookup<int, SVR_AniDB_Anime_Tag> GetByAnimeIDs(ISessionWrapper session, ICollection<int> ids)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Count == 0)
            {
                return EmptyLookup<int, SVR_AniDB_Anime_Tag>.Instance;
            }

            var tags = session.CreateCriteria<SVR_AniDB_Anime_Tag>()
                .Add(Restrictions.InG(nameof(SVR_AniDB_Anime_Tag.AnimeID), ids))
                .List<SVR_AniDB_Anime_Tag>()
                .ToLookup(t => t.AnimeID);

            return tags;
        }

        /// <summary>
        /// Gets all the anime tags, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<SVR_AniDB_Anime_Tag> GetAllForLocalSeries()
        {
            return RepoFactory.AnimeSeries.GetAll()
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
    }
}