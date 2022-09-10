using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories
{
    public class AniDB_TagRepository : BaseCachedRepository<AniDB_Tag, int>
    {
        private PocoIndex<int, AniDB_Tag, int> Tags;
        private PocoIndex<int, AniDB_Tag, string> Names;

        public override void PopulateIndexes()
        {
            Tags = new PocoIndex<int, AniDB_Tag, int>(Cache, a => a.TagID);
            Names = new PocoIndex<int, AniDB_Tag, string>(Cache, a => a.TagName);
        }

        protected override int SelectKey(AniDB_Tag entity) => entity.AniDB_TagID;

        public override void RegenerateDb()
        {
            var tags = Cache.Values.Where(tag => (tag.TagDescription?.Contains('`') ?? false) || tag.TagName.Contains('`')).ToList();
            foreach (var tag in tags)
            {
                tag.TagDescription = tag.TagDescription?.Replace('`', '\'');
                tag.TagName = tag.TagName.Replace('`', '\'');
                Save(tag);
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
            Lock.EnterReadLock();
            var result = Tags.GetOne(id);
            Lock.ExitReadLock();
            return result;
        }

        public List<AniDB_Tag> GetByName(string name)
        {
            Lock.EnterReadLock();
            var result = Names.GetMultiple(name);
            Lock.ExitReadLock();
            return result;
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
                .Where(a => a != null)
                .DistinctBy(a => a.TagID)
                .ToList();
        }
    }
}
