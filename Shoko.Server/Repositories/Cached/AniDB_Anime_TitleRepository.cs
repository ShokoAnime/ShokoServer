using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Commons.Properties;
using Shoko.Models.Server;
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

        protected override int SelectKey(AniDB_Anime_Title entity)
        {
            return entity.AniDB_Anime_TitleID;
        }

        public override void RegenerateDb()
        {
            ServerState.Instance.ServerStartingStatus = string.Format(
                Resources.Database_Validating, typeof(AniDB_Anime_Title).Name, " DbRegen");
            List<AniDB_Anime_Title> titles = Cache.Values.Where(title => title.Title.Contains('`')).ToList();
            foreach (AniDB_Anime_Title title in titles)
            {
                title.Title = title.Title.Replace('`', '\'');
                Save(title);
            }
        }


        public List<AniDB_Anime_Title> GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return Animes.GetMultiple(id);
            }
        }

        public ILookup<int, AniDB_Anime_Title> GetByAnimeIDs(ISessionWrapper session, ICollection<int> ids)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Count == 0)
            {
                return EmptyLookup<int, AniDB_Anime_Title>.Instance;
            }

            lock (globalDBLock)
            {
                var titles = session.CreateCriteria<AniDB_Anime_Title>()
                    .Add(Restrictions.InG(nameof(AniDB_Anime_Title.AnimeID), ids))
                    .List<AniDB_Anime_Title>()
                    .ToLookup(t => t.AnimeID);

                return titles;
            }
        }

        public List<AniDB_Anime_Title> GetByAnimeIDLanguageTypeValue(int animeID, string language, string titleType,
            string titleValue)
        {
            lock (Cache)
            {
                return Animes.GetMultiple(animeID).Where(a =>
                    a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase) &&
                    a.Title.Equals(titleValue, StringComparison.InvariantCultureIgnoreCase) &&
                    a.TitleType.Equals(titleType, StringComparison.InvariantCultureIgnoreCase)).ToList();
            }
        }

        /// <summary>
        /// Gets all the anime titles, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Anime_Title> GetAllForLocalSeries()
        {
            return RepoFactory.AnimeSeries.GetAll().SelectMany(a => GetByAnimeID(a.AniDB_ID)).Where(a => a != null)
                .Distinct().ToList();
        }
    }
}
