using JMMContracts;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
    public class IgnoreAnime
    {
        public int IgnoreAnimeID { get; private set; }
        public int JMMUserID { get; set; }
        public int AnimeID { get; set; }
        public int IgnoreType { get; set; }

        public override string ToString()
        {
            return string.Format("User: {0} - Anime: {1} - Type: {2}", JMMUserID, AnimeID, IgnoreType);
        }

        public Contract_IgnoreAnime ToContract()
        {
            var contract = new Contract_IgnoreAnime();

            contract.IgnoreAnimeID = IgnoreAnimeID;
            contract.JMMUserID = JMMUserID;
            contract.AnimeID = AnimeID;
            contract.IgnoreType = IgnoreType;

            var repAnime = new AniDB_AnimeRepository();
            var anime = repAnime.GetByAnimeID(AnimeID);
            if (anime != null) contract.Anime = anime.ToContract();

            return contract;
        }
    }
}