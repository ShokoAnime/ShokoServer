using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// Cross-reference between an AniDB Anime and an AniList Anime.
/// </summary>
public class CrossRef_AniDB_Anilist_Anime : IAnilistAnimeCrossReference
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int CrossRef_AniDB_Anilist_AnimeID { get; set; }

    /// <summary>
    /// The AniDB Anime ID.
    /// </summary>
    public int AnidbAnimeID { get; set; }

    /// <summary>
    /// The AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// The match rating for the cross-reference.
    /// </summary>
    public MatchRating MatchRating { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public CrossRef_AniDB_Anilist_Anime() { }

    /// <summary>
    /// Creates a new cross-reference.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="anilistAnimeId">The AniList anime ID.</param>
    /// <param name="matchRating">The match rating.</param>
    public CrossRef_AniDB_Anilist_Anime(int anidbAnimeId, int anilistAnimeId, MatchRating matchRating = MatchRating.UserVerified)
    {
        AnidbAnimeID = anidbAnimeId;
        AnilistAnimeID = anilistAnimeId;
        MatchRating = matchRating;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets the associated AnimeSeries.
    /// </summary>
    public AnimeSeries? AnimeSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    /// <summary>
    /// Gets the associated AniList Anime.
    /// </summary>
    public Anilist_Anime? AnilistAnime => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region IAnilistAnimeCrossReference Implementation

    IShokoSeries? IAnilistAnimeCrossReference.ShokoSeries => AnimeSeries;

    IAnilistAnime? IAnilistAnimeCrossReference.AnilistAnime => AnilistAnime;

    #endregion
}
