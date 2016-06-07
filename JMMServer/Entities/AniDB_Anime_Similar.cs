using AniDBAPI;
using JMMContracts;

namespace JMMServer.Entities
{
    public class AniDB_Anime_Similar
    {
        public int AniDB_Anime_SimilarID { get; private set; }
        public int AnimeID { get; set; }
        public int SimilarAnimeID { get; set; }
        public int Approval { get; set; }
        public int Total { get; set; }

        public double ApprovalPercentage
        {
            get
            {
                if (Total == 0) return 0;
                return Approval / (double)Total * 100;
            }
        }


        public void Populate(Raw_AniDB_SimilarAnime rawSim)
        {
            AnimeID = rawSim.AnimeID;
            Approval = rawSim.Approval;
            Total = rawSim.Total;
            SimilarAnimeID = rawSim.SimilarAnimeID;
        }

        public Contract_AniDB_Anime_Similar ToContract(AniDB_Anime anime, AnimeSeries ser, int userID)
        {
            var contract = new Contract_AniDB_Anime_Similar();

            contract.AniDB_Anime_SimilarID = AniDB_Anime_SimilarID;
            contract.AnimeID = AnimeID;
            contract.SimilarAnimeID = SimilarAnimeID;
            contract.Approval = Approval;
            contract.Total = Total;

            contract.AniDB_Anime = null;
            if (anime != null)
                contract.AniDB_Anime = anime.ToContract();

            contract.AnimeSeries = null;
            if (ser != null)
                contract.AnimeSeries = ser.ToContract(ser.GetUserRecord(userID));

            return contract;
        }
    }
}