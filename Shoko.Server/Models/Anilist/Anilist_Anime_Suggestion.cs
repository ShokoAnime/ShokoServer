using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// An anime AniList's users recommend to someone who liked another one.
/// AniList only deals in anime here, so both ends are anime. The recommended
/// anime is usually not in the collection, so only its AniList ID, the net
/// votes behind it and its place in AniList's list are kept.
/// </summary>
public class Anilist_Anime_Suggestion : IAnilistSuggestion
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_Anime_SuggestionID { get; set; }

    /// <summary>
    /// AniList Anime ID of the anime being looked at.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// AniList Anime ID of the recommended anime.
    /// </summary>
    public int SuggestedAnilistAnimeID { get; set; }

    /// <summary>
    /// AniList's net score for the recommendation: the votes for it minus the
    /// votes against, so it can be negative.
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// The entry's position in AniList's list, best first, starting at
    /// <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Constructors

    public Anilist_Anime_Suggestion() { }

    public Anilist_Anime_Suggestion(int anilistAnimeId, int suggestedAnilistAnimeId, int rating, int ordering)
    {
        AnilistAnimeID = anilistAnimeId;
        SuggestedAnilistAnimeID = suggestedAnilistAnimeId;
        Rating = rating;
        Ordering = ordering;
    }

    #endregion

    #region Computed Properties

    /// <summary>
    /// The anime being looked at, if it is in the collection.
    /// </summary>
    public Anilist_Anime? Anime => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// The recommended anime, if it is in the collection.
    /// </summary>
    public Anilist_Anime? SuggestedAnime => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(SuggestedAnilistAnimeID);

    #endregion

    #region ISuggestedMetadata Implementation

    int ISuggestedMetadata.BaseID => AnilistAnimeID;

    int ISuggestedMetadata.SuggestedID => SuggestedAnilistAnimeID;

    IMetadata<int>? ISuggestedMetadata.Base => Anime;

    IMetadata<int>? ISuggestedMetadata.Suggested => SuggestedAnime;

    // AniList only has "watch this next" recommendations.
    SuggestionKind ISuggestedMetadata.Kind => SuggestionKind.Recommended;

    int? ISuggestedMetadata.Order => Ordering;

    // A net score is not a share of the votes, so there is nothing to turn
    // into a percentage.
    double? ISuggestedMetadata.ApprovalRating => null;

    // AniList's rating is a net score rather than a count of voters, so there
    // is nothing to report here. Read Rating for the score itself.
    int? ISuggestedMetadata.Votes => null;

    DataSource ISuggestedMetadata.Source => DataSource.AniList;

    public bool Equals(ISuggestedMetadata? other)
        => other is not null && other.Source is DataSource.AniList &&
            other.BaseID == AnilistAnimeID && other.SuggestedID == SuggestedAnilistAnimeID;

    #endregion

    #region ISuggestedMetadata<IAnilistAnime> Implementation

    IAnilistAnime? ISuggestedMetadata<IAnilistAnime, IAnilistAnime>.Base => Anime;

    IAnilistAnime? ISuggestedMetadata<IAnilistAnime, IAnilistAnime>.Suggested => SuggestedAnime;

    #endregion
}
