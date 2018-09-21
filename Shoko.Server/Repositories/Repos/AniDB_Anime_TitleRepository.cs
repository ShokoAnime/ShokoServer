using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

// ReSharper disable All

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Anime_TitleRepository : BaseRepository<AniDB_Anime_Title, int>
    {
        private PocoIndex<int, AniDB_Anime_Title, int> Animes;

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Title, int>(Cache, a => a.AnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
        }

        internal override int SelectKey(AniDB_Anime_Title entity) => entity.AniDB_Anime_TitleID;

        public override void PreInit(IProgress<InitProgress> progress,int batchSize)
        {
            //TODO Do we still need to run this every time?
            List<AniDB_Anime_Title> titles = GetTitleContains("`");
            if (titles.Count == 0)
                return;
            InitProgress regen = new InitProgress();
            regen.Title = "Fixing Anime Titles";
            regen.Step = 0;
            regen.Total = titles.Count;
            progress.Report(regen);
            BatchAction(titles,batchSize, (update,original) =>
            {
                update.Title = update.Title.Replace('`', '\'');
                regen.Step++;
                progress.Report(regen);
            },null);
            regen.Step = regen.Total;
            progress.Report(regen);
        }



        public List<AniDB_Anime_Title> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<int> GetIdsByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_Anime_TitleID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a => a.AniDB_Anime_TitleID).ToList();
            }
        }
        public List<AniDB_Anime_Title> GetTitleContains(string str)
        {
            return Where(title => title.Title.Contains(str)).ToList();
        }

        public Dictionary<int, List<AniDB_Anime_Title>> GetByAnimeIDs(IEnumerable<int> ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ids.ToDictionary(a=>a,a=>Animes.GetMultiple(a));
                return Table.Where(a => ids.Contains(a.AnimeID)).GroupBy(a=>a.AnimeID).ToDictionary(a=>a.Key,a=>a.ToList());
            }
        }
    
        public AniDB_Anime_Title GetByAnimeIDLanguageTypeValue(int animeID, string language, string titleType, string title)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID).FirstOrDefault(a =>
                        a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase) &&
                        a.Title.Equals(title, StringComparison.InvariantCultureIgnoreCase) &&
                        a.TitleType.Equals(titleType, StringComparison.InvariantCultureIgnoreCase));

                return Table.FirstOrDefault(a => a.AnimeID == animeID &&
                                        a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase) &&
                                        a.Title.Equals(title, StringComparison.InvariantCultureIgnoreCase) &&
                                        a.TitleType.Equals(titleType, StringComparison.InvariantCultureIgnoreCase));
            }
        }
        /// <summary>t
        /// Gets all the anime titles, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Anime_Title> GetAllForLocalSeries()
        {
            return GetByAnimeIDs(Repo.Instance.AnimeSeries.GetAllAnimeIds()).SelectMany(a => a.Value).Distinct().ToList();
        }
    }
}