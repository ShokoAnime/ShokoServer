
# nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Similar
{
    public int AniDB_Anime_SimilarID { get; set; }

    public int AnimeID { get; set; }

    public int SimilarAnimeID { get; set; }

    public int Approval { get; set; }

    public int Total { get; set; }
}
