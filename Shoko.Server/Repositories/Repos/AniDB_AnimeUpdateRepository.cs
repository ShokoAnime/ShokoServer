using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_AnimeUpdateRepository : BaseRepository<AniDB_AnimeUpdate, int>
    {        public AniDB_AnimeUpdate GetByAnimeID(int id)
        {

            using (RepoLock.ReaderLock())
            {
                return Table.FirstOrDefault(a => a.AnimeID == id);
            }

            /*var cats = Table
                .CreateCriteria(typeof(AniDB_AnimeUpdate))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_AnimeUpdate>();

            var cat = cats.FirstOrDefault();
            cats.Remove(cat);
            if (cats.Count > 1) cats.ForEach(Delete);

            return cat;*/
        }

        internal override void ClearIndexes()
        {
            
        }

        internal override void PopulateIndexes()
        {
            
        }

        internal override int SelectKey(AniDB_AnimeUpdate entity) => entity.AniDB_AnimeUpdateID;
    }
}