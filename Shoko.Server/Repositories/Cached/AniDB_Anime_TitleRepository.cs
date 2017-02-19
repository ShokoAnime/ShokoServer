using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using FluentNHibernate.Utils;
using Shoko.Models.Server;
using Shoko.Server.Repositories.Cached;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    public class AniDB_Anime_TitleRepository : BaseCachedRepository<AniDB_Anime_Title, int>
    {
        private PocoIndex<int, AniDB_Anime_Title, int> Animes;
        public override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Title, int>(Cache, a => a.AnimeID);
        }

        private AniDB_Anime_TitleRepository() { }
        public static AniDB_Anime_TitleRepository Create()
        {
            return new AniDB_Anime_TitleRepository();
        }

        protected override int SelectKey(AniDB_Anime_Title entity)
        {
            return entity.AniDB_Anime_TitleID;
        }

        public override void RegenerateDb() { }



        public List<AniDB_Anime_Title> GetByAnimeID(int id)
        {
            return Animes.GetMultiple(id);
        }

        public ILookup<int, AniDB_Anime_Title> GetByAnimeIDs(ISessionWrapper session, ICollection<int> ids)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (ids == null)
                throw new ArgumentNullException("ids");

            if (ids.Count == 0)
            {
                return EmptyLookup<int, AniDB_Anime_Title>.Instance;
            }

            var titles = session.CreateCriteria<AniDB_Anime_Title>()
                .Add(Restrictions.InG(nameof(AniDB_Anime_Title.AnimeID), ids))
                .List<AniDB_Anime_Title>()
                .ToLookup(t => t.AnimeID);

            return titles;
        }

        public List<AniDB_Anime_Title> GetByAnimeIDLanguageTypeValue(int animeID, string language, string titleType,
            string titleValue)
        {
            return
                Animes.GetMultiple(animeID)
                    .Where(
                        a =>
                            a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase) &&
                            a.Title.Equals(titleValue, StringComparison.InvariantCultureIgnoreCase) &&
                            a.TitleType.Equals(titleType, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();
            /*
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
            }*/
        }

        /// <summary>
        /// Gets all the anime titles, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Anime_Title> GetAllForLocalSeries()
        {
            return
                RepoFactory.AnimeSeries.GetAll()
                    .SelectMany(a => GetByAnimeID(a.AniDB_ID))
                    .Where(a => a != null)
                    .Distinct()
                    .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var titles =
                    session.CreateQuery(
                        "FROM AniDB_Anime_Title aat WHERE aat.AnimeID IN (Select aser.AniDB_ID From AnimeSeries aser)")
                        .List<AniDB_Anime_Title>();

                return new List<AniDB_Anime_Title>(titles);
            }*/
        }

    }
}