using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_CustomTagRepository : BaseRepository<CrossRef_CustomTag, int>
    {
        private PocoIndex<int, CrossRef_CustomTag, int> Tags;
        private PocoIndex<int, CrossRef_CustomTag, int, int> Refs;


        internal override int SelectKey(CrossRef_CustomTag entity) => entity.CrossRef_CustomTagID;

        internal override void PopulateIndexes()
        {
            Tags = new PocoIndex<int, CrossRef_CustomTag, int>(Cache, a => a.CustomTagID);
            Refs = new PocoIndex<int, CrossRef_CustomTag, int, int>(Cache, a => a.CrossRefID, a => a.CrossRefType);
        }

        internal override void ClearIndexes()
        {
            Tags = null;
            Refs = null;
        }



        public List<CrossRef_CustomTag> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Refs.GetMultiple(id, (int)CustomTagCrossRefType.Anime);
                return Table.Where(a => a.CrossRefID==id && a.CrossRefType== (int)CustomTagCrossRefType.Anime).ToList();
            }
        }


        public List<CrossRef_CustomTag> GetByCustomTagID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Tags.GetMultiple(id);
                return Table.Where(a => a.CustomTagID==id).ToList();
            }
        }


        public List<CrossRef_CustomTag> GetByUniqueID(int customTagID, int crossRefType, int crossRefID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Refs.GetMultiple(crossRefID, crossRefType).Where(a => a.CustomTagID == customTagID).ToList(); 
                return Table.Where(a => a.CrossRefID == crossRefID && a.CrossRefType == crossRefType && a.CustomTagID==customTagID).ToList();
            }
        }
    }
}