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
            Contract_BookmarkedAnime contract = new Contract_BookmarkedAnime();

            contract.BookmarkedAnimeID = BookmarkedAnimeID;
            contract.AnimeID = AnimeID;
            contract.Priority = Priority;
            contract.Notes = Notes;
            contract.Downloading = Downloading;

            contract.Anime = null;
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            AniDB_Anime an = repAnime.GetByAnimeID(AnimeID);
            if (an != null)
                contract.Anime = an.Contract.AniDBAnime;

            return contract;
        }
    }
}