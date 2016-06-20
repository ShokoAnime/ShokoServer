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
                if (Total == 0) return (double) 0;
                return (double) Approval/(double) Total*(double) 100;
            }
        }


        public void Populate(Raw_AniDB_SimilarAnime rawSim)
        {
            this.AnimeID = rawSim.AnimeID;
            this.Approval = rawSim.Approval;
            this.Total = rawSim.Total;
            this.SimilarAnimeID = rawSim.SimilarAnimeID;
        }

        public Contract_AniDB_Anime_Similar ToContract(AniDB_Anime anime, AnimeSeries ser, int userID)
        {
            Contract_AniDB_Anime_Similar contract = new Contract_AniDB_Anime_Similar();

            contract.AniDB_Anime_SimilarID = this.AniDB_Anime_SimilarID;
            contract.AnimeID = this.AnimeID;
            contract.SimilarAnimeID = this.SimilarAnimeID;
            contract.Approval = this.Approval;
            contract.Total = this.Total;
            contract.AniDB_Anime = anime?.Contract?.AniDBAnime;
            contract.AnimeSeries = ser?.GetUserContract(userID);
            return contract;
        }
    }
}