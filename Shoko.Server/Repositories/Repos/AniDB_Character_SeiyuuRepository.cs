using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Character_SeiyuuRepository : BaseRepository<AniDB_Character_Seiyuu, int>
    {
        private PocoIndex<int, AniDB_Character_Seiyuu, int> Chars;
        private PocoIndex<int, AniDB_Character_Seiyuu, int> Seiyuus;
        private PocoIndex<int, AniDB_Character_Seiyuu, int, int> CharSeiyuus;

        internal override int SelectKey(AniDB_Character_Seiyuu entity) => entity.AniDB_Character_SeiyuuID;

        internal override void PopulateIndexes()
        {
            Chars = new PocoIndex<int, AniDB_Character_Seiyuu, int>(Cache, a => a.CharID);
            Seiyuus = new PocoIndex<int, AniDB_Character_Seiyuu, int>(Cache, a => a.SeiyuuID);
            CharSeiyuus = new PocoIndex<int, AniDB_Character_Seiyuu, int, int>(Cache, a => a.CharID, a => a.SeiyuuID);
        }

        internal override void ClearIndexes()
        {
            Chars = null;
            Seiyuus = null;
            CharSeiyuus = null;
        }


        public AniDB_Character_Seiyuu GetByCharIDAndSeiyuuID(int charid, int seiyuuid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CharSeiyuus.GetOne(charid, seiyuuid);
                return Table.FirstOrDefault(a => a.CharID == charid && a.SeiyuuID==seiyuuid);
            }
        }

        public List<AniDB_Character_Seiyuu> GetByCharID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Chars.GetMultiple(id);
                return Table.Where(a => a.CharID == id).ToList();
            }

        }
        public List<int> GetIdsByCharID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Chars.GetMultiple(id).Select(a=>a.AniDB_Character_SeiyuuID).ToList();
                return Table.Where(a => a.CharID == id).Select(a => a.AniDB_Character_SeiyuuID).ToList();
            }

        }

        public List<AniDB_Character_Seiyuu> GetBySeiyuuID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Seiyuus.GetMultiple(id);
                return Table.Where(a => a.SeiyuuID == id).ToList();
            }
        }
        public Dictionary<int, List<int>> GetSeiyuusFromCharIds(IEnumerable<int> charids)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return charids.ToDictionary(a => a, a => Chars.GetMultiple(a).Select(b => b.SeiyuuID).ToList());
                return Table.Where(a => charids.Contains(a.CharID)).GroupBy(a => a.CharID).ToDictionary(a => a.Key, a => a.Select(b => b.SeiyuuID).ToList());
            }
        }



    }
}