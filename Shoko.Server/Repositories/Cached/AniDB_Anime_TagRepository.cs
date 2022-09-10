using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories
{
    public class AniDB_Anime_TagRepository : BaseCachedRepository<AniDB_Anime_Tag, int>
    {
        private PocoIndex<int, AniDB_Anime_Tag, int> Animes;
        private PocoIndex<int, AniDB_Anime_Tag, int> TagIDs;

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(AniDB_Anime_Tag entity)
        {
            return entity.AniDB_Anime_TagID;
        }

        public override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Tag, int>(Cache, a => a.AnimeID);
            TagIDs = new PocoIndex<int, AniDB_Anime_Tag, int>(Cache, a => a.TagID);
        }

        public AniDB_Anime_Tag GetByAnimeIDAndTagID(int animeid, int tagid)
        {
            Lock.EnterReadLock();
            var result = Animes.GetMultiple(animeid).FirstOrDefault(a => a.TagID == tagid);
            Lock.ExitReadLock();
            return result;
        }

        public List<AniDB_Anime_Tag> GetByAnimeID(int id)
        {
            Lock.EnterReadLock();
            var result = Animes.GetMultiple(id);
            Lock.ExitReadLock();
            return result;
        }

        public ILookup<int, AniDB_Anime_Tag> GetByAnimeIDs(ICollection<int> ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Count == 0)
            {
                return EmptyLookup<int, AniDB_Anime_Tag>.Instance;
            }

            Lock.EnterReadLock();
            var result = ids.SelectMany(Animes.GetMultiple).ToLookup(t => t.AnimeID);
            Lock.ExitReadLock();
            return result;
        }

        public List<SVR_AnimeSeries> GetAnimeWithTag(string tagName)
        {
            return GetAll().AsParallel().Where(a => RepoFactory.AniDB_Tag.GetByName(tagName).Select(b => b.TagID).Contains(a.TagID))
                .Select(a => RepoFactory.AnimeSeries.GetByAnimeID(a.AnimeID))
                .Where(a => a != null)
                .ToList();
        }

        public List<SVR_AnimeSeries> GetAnimeWithTag(int tagID)
        {
            return GetByTagID(tagID).Select(a => RepoFactory.AnimeSeries.GetByAnimeID(a.AnimeID))
                .Where(a => a != null).ToList();
        }

        public List<AniDB_Anime_Tag> GetByTagID(int tagID)
        {
            Lock.EnterReadLock();
            var result = TagIDs.GetMultiple(tagID);
            Lock.ExitReadLock();
            return result;
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
