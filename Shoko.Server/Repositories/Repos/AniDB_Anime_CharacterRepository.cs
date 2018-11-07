using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Anime_CharacterRepository : BaseRepository<AniDB_Anime_Character, int>
    {

        private PocoIndex<int, AniDB_Anime_Character, int> Animes;
        private PocoIndex<int, AniDB_Anime_Character, int> Chars;
        private PocoIndex<int, AniDB_Anime_Character, int, int> AnimeChars;

        internal override int SelectKey(AniDB_Anime_Character entity) => entity.AniDB_Anime_CharacterID;

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Character, int>(Cache, a => a.AnimeID);
            Chars = new PocoIndex<int, AniDB_Anime_Character, int>(Cache, a => a.CharID);
            AnimeChars = new PocoIndex<int, AniDB_Anime_Character, int, int>(Cache,a=>a.AnimeID,a=>a.CharID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            Chars = null;
            AnimeChars = null;
        }


        public List<AniDB_Anime_Character> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<int> GetIdsByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_Anime_CharacterID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a => a.AniDB_Anime_CharacterID).ToList();
            }
        }
        public Dictionary<int,List<(int,string)>> GetCharsByAnimesIDs(IEnumerable<int> animeids)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return animeids.ToDictionary(a=>a, a => Animes.GetMultiple(a).Select(b=>(b.CharID,b.CharType)).ToList());
                return Table.Where(a => animeids.Contains(a.AnimeID)).GroupBy(a => a.AnimeID).ToDictionary(a => a.Key, a => a.Select(b => (b.CharID,b.CharType)).ToList());
            }
        }

        public List<AniDB_Anime_Character> GetByCharID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Chars.GetMultiple(id);
                return Table.Where(a => a.CharID == id).ToList();
            }
        }

        public AniDB_Anime_Character GetByAnimeIDAndCharID(int animeid, int charid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeChars.GetOne(animeid, charid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.CharID == charid);
            }
        }
    }
}