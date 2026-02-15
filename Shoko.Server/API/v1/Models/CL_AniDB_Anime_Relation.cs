namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_Anime_Relation
{
    public int AniDB_Anime_RelationID { get; set; }
    public int AnimeID { get; set; }
    public string RelationType { get; set; }
    public int RelatedAnimeID { get; set; }
    public CL_AniDB_Anime AniDB_Anime { get; set; }
    public CL_AnimeSeries_User AnimeSeries { get; set; }

}
