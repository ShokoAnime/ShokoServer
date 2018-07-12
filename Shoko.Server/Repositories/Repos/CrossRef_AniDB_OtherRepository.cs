using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_OtherRepository : BaseRepository<CrossRef_AniDB_Other, int>
    {
        private PocoIndex<int, CrossRef_AniDB_Other, int> Animes;
        private PocoIndex<int, CrossRef_AniDB_Other, int> CrossTypes;
        internal override int SelectKey(CrossRef_AniDB_Other entity) => entity.CrossRef_AniDB_OtherID;

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, CrossRef_AniDB_Other, int>(Cache, a => a.AnimeID);
            CrossTypes = new PocoIndex<int, CrossRef_AniDB_Other, int>(Cache, a => a.CrossRefType);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            CrossTypes = null;
        }

        public CrossRef_AniDB_Other GetByAnimeIDAndType(int animeID, CrossRefType xrefType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID).FirstOrDefault(a=>a.CrossRefType==(int)xrefType);
                return Table.FirstOrDefault(a => a.AnimeID == animeID && a.CrossRefType == (int)xrefType);
            }
        }


        public Dictionary<int, List<CrossRef_AniDB_Other>> GetByAnimeIDsAndTypes(IEnumerable<int> animeIds, params CrossRefType[] xrefTypes)
        {
            if (xrefTypes == null || xrefTypes.Length == 0 || animeIds == null)
                return new Dictionary<int, List<CrossRef_AniDB_Other>>();
            List<int> types = xrefTypes.Cast<int>().ToList();
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return animeIds.ToDictionary(a=>a,a => Animes.GetMultiple(a).Where(b => types.Contains(b.CrossRefType)).ToList());
                return Table.Where(a => animeIds.Contains(a.AnimeID) && types.Contains(a.CrossRefType)).GroupBy(a=>a.AnimeID).ToDictionary(a=>a.Key,a=>a.ToList());
            }
        }

        public Dictionary<int, List<CrossRef_AniDB_Other>> GetByAnimeIDsAndType(IEnumerable<int> animeIds, CrossRefType type)
        {
            if (animeIds == null)
                return new Dictionary<int, List<CrossRef_AniDB_Other>>();
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return animeIds.ToDictionary(a=>a, a => Animes.GetMultiple(a).Where(b => b.CrossRefType==(int)type).Select(b=>b).ToList());
                return Table.Where(a => animeIds.Contains(a.AnimeID) && a.CrossRefType==(int)type).GroupBy(a=>a.AnimeID).ToDictionary(a=>a.Key, a=>a.Select(b=>b).ToList());
            }
        }

        public List<CrossRef_AniDB_Other> GetByType(CrossRefType xrefType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CrossTypes.GetMultiple((int)xrefType);
                return Table.Where(a => a.CrossRefType == (int)xrefType).ToList();
            }
        }

        public List<CrossRef_AniDB_Other> GetByAnimeID(int animeID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID);
                return Table.Where(a => a.AnimeID == animeID).ToList();
            }
        }
    }
}