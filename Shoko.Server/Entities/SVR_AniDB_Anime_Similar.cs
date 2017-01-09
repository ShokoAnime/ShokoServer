using AniDBAPI;
using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Anime_Similar : AniDB_Anime_Similar
    {


        public void Populate(Raw_AniDB_SimilarAnime rawSim)
        {
            this.AnimeID = rawSim.AnimeID;
            this.Approval = rawSim.Approval;
            this.Total = rawSim.Total;
            this.SimilarAnimeID = rawSim.SimilarAnimeID;
        }

        public CL_AniDB_Anime_Similar ToClient(SVR_AniDB_Anime anime, SVR_AnimeSeries ser, int userID)
        {
            CL_AniDB_Anime_Similar cl = this.CloneToClient();
            cl.AniDB_Anime = anime?.Contract?.AniDBAnime;
            cl.AnimeSeries = ser?.GetUserContract(userID);
            return cl;
        }
    }
}