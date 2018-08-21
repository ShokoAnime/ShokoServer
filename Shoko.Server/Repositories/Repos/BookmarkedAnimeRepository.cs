using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class BookmarkedAnimeRepository : BaseRepository<BookmarkedAnime, int>
    {

        private PocoIndex<int, BookmarkedAnimeRepository, int> AnimeIDs;

        internal override int SelectKey(BookmarkedAnime entity) => entity.AnimeID;

        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }

        public override List<BookmarkedAnime> GetAll()
        {
            return base.GetAll().OrderBy(a => a.Priority).ToList();
        }

        internal BookmarkedAnime GetByAnimeID(int id)
        {
            //TODO: Cached version
            return Table.FirstOrDefault(s => s.AnimeID == id);
        }
    }
}