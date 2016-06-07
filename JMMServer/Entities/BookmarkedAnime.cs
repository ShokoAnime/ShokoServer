using JMMContracts;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
    public class BookmarkedAnime
    {
        public int BookmarkedAnimeID { get; private set; }
        public int AnimeID { get; set; }
        public int Priority { get; set; }
        public string Notes { get; set; }
        public int Downloading { get; set; }


        public Contract_BookmarkedAnime ToContract()
        {
            var contract = new Contract_BookmarkedAnime();

            contract.BookmarkedAnimeID = BookmarkedAnimeID;
            contract.AnimeID = AnimeID;
            contract.Priority = Priority;
            contract.Notes = Notes;
            contract.Downloading = Downloading;

            contract.Anime = null;
            var repAnime = new AniDB_AnimeRepository();
            var an = repAnime.GetByAnimeID(AnimeID);
            if (an != null)
                contract.Anime = an.ToContract(true, null);

            return contract;
        }
    }
}