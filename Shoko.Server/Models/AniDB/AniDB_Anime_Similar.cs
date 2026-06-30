
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Similar : IAnidbSimilarAnime
{
    public int AniDB_Anime_SimilarID { get; set; }

    public int AnimeID { get; set; }

    public int SimilarAnimeID { get; set; }

    public int Approval { get; set; }

    public int Total { get; set; }

    #region IAnidbSimilarAnime Implementation

    int IAnidbSimilarAnime.BaseID => AnimeID;

    int IAnidbSimilarAnime.SimilarID => SimilarAnimeID;

    double IAnidbSimilarAnime.ApprovalRating => Approval / (double)Total * 100;

    int IAnidbSimilarAnime.ApprovalVotes => Approval;

    int IAnidbSimilarAnime.TotalVotes => Total;

    IAnidbAnime? IAnidbSimilarAnime.Base => RepoFactory.AniDB_Anime.GetByID(AnimeID);

    IAnidbAnime? IAnidbSimilarAnime.Similar => RepoFactory.AniDB_Anime.GetByID(SimilarAnimeID);

    #endregion
}
