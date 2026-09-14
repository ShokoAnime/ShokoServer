using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// Cross-reference between an AniDB Episode and an AniList Episode.
/// </summary>
public class CrossRef_AniDB_Anilist_Episode : IAnilistEpisodeCrossReference
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int CrossRef_AniDB_Anilist_EpisodeID { get; set; }

    /// <summary>
    /// The AniDB Anime ID.
    /// </summary>
    public int AnidbAnimeID { get; set; }

    /// <summary>
    /// The AniDB Episode ID.
    /// </summary>
    public int AnidbEpisodeID { get; set; }

    /// <summary>
    /// The AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// The synthesized AniList Episode ID. See
    /// <see cref="Providers.Anilist.AnilistUtility.PackEpisodeID"/>.
    /// </summary>
    public int AnilistEpisodeID { get; set; }

    /// <summary>
    /// The episode number in AniList.
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// The ordering index for multiple links to the same episode.
    /// </summary>
    public int Ordering { get; set; }

    /// <summary>
    /// The match rating for the cross-reference.
    /// </summary>
    public MatchRating MatchRating { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public CrossRef_AniDB_Anilist_Episode() { }

    /// <summary>
    /// Creates a new cross-reference.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="anidbEpisodeId">The AniDB episode ID.</param>
    /// <param name="anilistAnimeId">The AniList anime ID.</param>
    /// <param name="anilistEpisodeId">The synthesized AniList episode ID.</param>
    /// <param name="episodeNumber">The episode number.</param>
    /// <param name="matchRating">The match rating.</param>
    public CrossRef_AniDB_Anilist_Episode(
        int anidbAnimeId,
        int anidbEpisodeId,
        int anilistAnimeId,
        int anilistEpisodeId,
        int episodeNumber,
        MatchRating matchRating = MatchRating.UserVerified)
    {
        AnidbAnimeID = anidbAnimeId;
        AnidbEpisodeID = anidbEpisodeId;
        AnilistAnimeID = anilistAnimeId;
        AnilistEpisodeID = anilistEpisodeId;
        EpisodeNumber = episodeNumber;
        MatchRating = matchRating;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets the associated AnimeSeries.
    /// </summary>
    public AnimeSeries? AnimeSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    /// <summary>
    /// Gets the associated AniDB episode.
    /// </summary>
    public AniDB_Episode? AnidbEpisode => RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    /// <summary>
    /// Gets the associated Shoko episode.
    /// </summary>
    public AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    /// <summary>
    /// Gets the associated AniList Anime.
    /// </summary>
    public Anilist_Anime? AnilistAnime => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets the associated AniList Episode.
    /// </summary>
    public Anilist_Episode? AnilistEpisode => AnilistEpisodeID is 0 ? null : RepoFactory.Anilist_Episode.GetByAnilistEpisodeID(AnilistEpisodeID);

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region IAnilistEpisodeCrossReference Implementation

    IShokoSeries? IAnilistEpisodeCrossReference.ShokoSeries => AnimeSeries;

    IAnilistAnime? IAnilistEpisodeCrossReference.AnilistAnime => AnilistAnime;

    IAnilistEpisode? IAnilistEpisodeCrossReference.AnilistEpisode => AnilistEpisode;

    #endregion
}
