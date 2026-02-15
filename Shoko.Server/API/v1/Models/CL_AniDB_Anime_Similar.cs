namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_Anime_Similar
{
    public int AniDB_Anime_SimilarID { get; set; }
    public int AnimeID { get; set; }
    public int SimilarAnimeID { get; set; }
    public int Approval { get; set; }
    public int Total { get; set; }
    public CL_AniDB_Anime AniDB_Anime { get; set; }
    public CL_AnimeSeries_User AnimeSeries { get; set; }
}
