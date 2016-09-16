using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using System.Linq;
using FluentNHibernate.Utils;
using JMMServer.Entities;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AniDB_Anime_TitleRepository : BaseCachedRepository<AniDB_Anime_Title, int>
    {
        private PocoIndex<int, AniDB_Anime_Title, int> Animes;
        public override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Title, int>(Cache, a => a.AnimeID);
        }

        public override void RegenerateDb()
        {
        }



        public List<AniDB_Anime_Title> GetByAnimeID(int id)
        {
            return Animes.GetMultiple(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var titles = session
                    .CreateCriteria(typeof(AniDB_Anime_Title))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<AniDB_Anime_Title>();

                return new List<AniDB_Anime_Title>(titles);
            }*/
        }

        public List<AniDB_Anime_Title> GetByAnimeID(ISessionWrapper session, int id)
        {
            return Animes.GetMultiple(id);
            /*
            var titles = session
                .CreateCriteria(typeof(AniDB_Anime_Title))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Anime_Title>();

            return new List<AniDB_Anime_Title>(titles);*/
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