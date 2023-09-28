using Shoko.Models.Enums;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Episode
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_EpisodeID { get; set; }

    public int AnidbEpisodeID { get; set; }

    public int TmdbEpisodeID { get; set; }

    public int Ordering { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Episode() { }

    public CrossRef_AniDB_TMDB_Episode(int anidbEpisodeId, int tmdbEpisodeId, int order = 1, MatchRating rating = MatchRating.UserVerified)
    {
        AnidbEpisodeID = anidbEpisodeId;
        TmdbEpisodeID = tmdbEpisodeId;
        Ordering = order;
        MatchRating = rating;
    }

    #endregion
}
