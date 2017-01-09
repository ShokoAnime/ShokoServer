using AniDBAPI;
using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Anime_Relation : AniDB_Anime_Relation
    {
        public void Populate(Raw_AniDB_RelatedAnime rawRel)
        {
            this.AnimeID = rawRel.AnimeID;
            this.RelatedAnimeID = rawRel.RelatedAnimeID;
            this.RelationType = rawRel.RelationType;
        }

        public CL_AniDB_Anime_Relation ToClient(SVR_AniDB_Anime anime, SVR_AnimeSeries ser, int userID)
        {
            CL_AniDB_Anime_Relation cl = this.CloneToClient();
            cl.AniDB_Anime = anime?.Contract?.AniDBAnime;
            cl.AnimeSeries = ser?.GetUserContract(userID);
            return cl;
        }
    }
}