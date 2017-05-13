using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    public class AniDB_TagRepository : BaseCachedRepository<AniDB_Tag, int>
    {
        private PocoIndex<int, AniDB_Tag, int> Tags;

        public override void PopulateIndexes()
        {
            Tags = new PocoIndex<int, AniDB_Tag, int>(Cache, a => a.TagID);
        }

        private AniDB_TagRepository()
        {
        }

        public static AniDB_TagRepository Create()
        {
            return new AniDB_TagRepository();
        }

        protected override int SelectKey(AniDB_Tag entity)
        {
            return entity.AniDB_TagID;
        }

        public override void RegenerateDb()
        {
        }

        public List<AniDB_Tag> GetByAnimeID(int animeID)
        {
            return RepoFactory.AniDB_Anime_Tag.GetByAnimeID(animeID)
                .Select(a => GetByTagID(a.TagID))
                .Where(a => a != null)
                .ToList();
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
                .CreateQuery(
                    "Select xref.AnimeID, tag FROM AniDB_Tag as tag, AniDB_Anime_Tag as xref WHERE tag.TagID = xref.TagID AND xref.AnimeID IN (:animeIDs)")
                .SetParameterList("animeIDs", ids)
                .List<object[]>()
                .ToLookup(t => (int) t[0], t => (AniDB_Tag) t[1]);

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
            return RepoFactory.AnimeSeries.GetAll()
                .SelectMany(a => RepoFactory.AniDB_Anime_Tag.GetByAnimeID(a.AniDB_ID))
                .Where(a => a != null)
                .Select(a => GetByTagID(a.TagID))
                .Distinct()
                .ToList();
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
    }
}