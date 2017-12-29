using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;

namespace Shoko.Server.RepositoriesV2.Repos
{
    public class AniDB_Anime_TagRepository : BaseRepository<AniDB_Anime_Tag, int>
    {
        private PocoIndex<int, AniDB_Anime_Tag, int> Animes;
        internal override int SelectKey(AniDB_Anime_Tag entity) => entity.AniDB_Anime_TagID;

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Tag, int>(Cache, a => a.AnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
        }

        public AniDB_Anime_Tag GetByAnimeIDAndTagID(int animeid, int tagid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeid).FirstOrDefault(a => a.TagID == tagid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.TagID == tagid);
            }
        }
        public List<AniDB_Anime_Tag> GetByAnimeIDAsync(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public ILookup<int, AniDB_Anime_Tag> GetByAnimeIDs(ICollection<int> ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Count == 0)
                return EmptyLookup<int, AniDB_Anime_Tag>.Instance;
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ids.SelectMany(Animes.GetMultiple).ToLookup(t => t.AnimeID);
                return Table.Where(a => ids.Contains(a.AnimeID)).ToLookup(t => t.AnimeID);
            }
        }
        /// <summary>
        /// Gets all the anime tags, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Anime_Tag> GetAllForLocalSeries()
        {
            return RepoFactory.AnimeSeries.GetAll()
                .SelectMany(a => GetByAnimeID(a.AniDB_ID))
                .Where(a => a != null)
                .Distinct()
                .ToList();
        }

    }
}