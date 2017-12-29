using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;

namespace Shoko.Server.RepositoriesV2.Repos
{
    public class AniDB_TagRepository : BaseRepository<AniDB_Tag, int>
    {
        private PocoIndex<int, AniDB_Tag, int> Tags;

        internal override void PopulateIndexes()
        {
            Tags = new PocoIndex<int, AniDB_Tag, int>(Cache, a => a.TagID);
        }

        internal override void ClearIndexes()
        {
            Tags = null;
        }

        private AniDB_TagRepository()
        {
        }


        internal override int SelectKey(AniDB_Tag entity) => entity.TagID;

        internal override void RegenerateDb(IProgress<RegenerateProgress> progress)
        {
            List<AniDB_Tag> tags=Where(tag => (tag.TagDescription?.Contains('`') ?? false) || tag.TagName.Contains('`')).ToList();
            using (IAtomic<List<AniDB_Tag>,object> update = BeginAtomicBatchUpdate(tags))
            {
                RegenerateProgress regen = new RegenerateProgress();
                regen.Title = "Fixing Tag Names";
                regen.Step = 0;
                regen.Total = update.Updatable.Count;
                foreach (AniDB_Tag tag in update.Updatable)
                {
                    tag.TagDescription = tag.TagDescription?.Replace('`', '\'');
                    tag.TagName = tag.TagName.Replace('`', '\'');
                    regen.Step++;
                    progress.Report(regen);
                }
                update.Commit();
                regen.Step = regen.Total;
                progress.Report(regen);
            }
        }




        public List<AniDB_Tag> GetByAnimeID(int animeID)
        {
            return RepoFactory.AniDB_Anime_Tag.GetByAnimeID(animeID)
                .Select(a => GetByTagID(a.TagID))
                .Where(a => a != null)
                .ToList();
        }


        public ILookup<int, AniDB_Tag> GetByAnimeIDs(int[] ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Length == 0)
            {
                return EmptyLookup<int, AniDB_Tag>.Instance;
            }

            return RepoFactory.AniDB_Anime_Tag.GetByAnimeIDs(ids).SelectMany(a => a.ToList())
                .ToLookup(t => t.AnimeID, t => GetByTagID(t.TagID));
        }


        public AniDB_Tag GetByTagID(int id)
        {
            using(CacheLock.ReaderLock())
            {
                return IsCached ? Tags.GetOne(id) : Table.FirstOrDefault(a => a.TagID == id);
            }
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
        }
    }
}