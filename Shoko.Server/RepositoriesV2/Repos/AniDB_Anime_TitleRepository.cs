using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories;
// ReSharper disable All

namespace Shoko.Server.RepositoriesV2.Repos
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

        public override void Init(IProgress<RegenerateProgress> progress,int batchSize)
        {
            //TODO Do we still need to run this every time?
            List<AniDB_Anime_Title> titles = GetTitleContains("`");
            if (titles.Count == 0)
                return;
            RegenerateProgress regen = new RegenerateProgress();
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
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<AniDB_Anime_Title> GetTitleContains(string str)
        {
            return Where(title => title.Title.Contains(str)).ToList();
        }

        public ILookup<int, AniDB_Anime_Title> GetByAnimeIDs(ICollection<int> ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));
            if (ids.Count == 0)
                return EmptyLookup<int, AniDB_Anime_Title>.Instance;
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ids.SelectMany(Animes.GetMultiple).ToLookup(t => t.AnimeID);
                return Table.Where(a => ids.Contains(a.AnimeID)).ToLookup(t => t.AnimeID);
            }
        }
    
        public List<AniDB_Anime_Title> GetByAnimeIDLanguageTypeValue(int animeID, string language, string titleType,
            string title)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID).Where(a =>
                        a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase) &&
                        a.Title.Equals(title, StringComparison.InvariantCultureIgnoreCase) &&
                        a.TitleType.Equals(titleType, StringComparison.InvariantCultureIgnoreCase)).ToList();

                return Table.Where(a => a.AnimeID == animeID &&
                                        a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase) &&
                                        a.Title.Equals(title, StringComparison.InvariantCultureIgnoreCase) &&
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