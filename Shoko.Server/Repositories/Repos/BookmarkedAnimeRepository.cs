using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class BookmarkedAnimeRepository : BaseRepository<BookmarkedAnime, int>
    {

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

    }
}