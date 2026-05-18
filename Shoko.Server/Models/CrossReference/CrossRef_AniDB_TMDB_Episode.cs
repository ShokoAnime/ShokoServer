using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Episode : ITmdbEpisodeCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_EpisodeID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int AnidbEpisodeID { get; set; }

    public int TmdbShowID { get; set; }

    public int TmdbEpisodeID { get; set; }

    public int Ordering { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion

    #region Constructors

    public CrossRef_AniDB_TMDB_Episode() { }

    public CrossRef_AniDB_TMDB_Episode(int anidbEpisodeId, int anidbAnimeId, int tmdbEpisodeId, int tmdbShowId, MatchRating rating = MatchRating.UserVerified, int ordering = 0)
    {
        AnidbEpisodeID = anidbEpisodeId;
        AnidbAnimeID = anidbAnimeId;
        TmdbEpisodeID = tmdbEpisodeId;
        TmdbShowID = tmdbShowId;
        Ordering = ordering;
        MatchRating = rating;
    }

    #endregion

    #region Methods

    public AniDB_Episode? AnidbEpisode =>
        RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeEpisode? AnimeEpisode =>
        RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Episode? TmdbEpisode =>
        TmdbEpisodeID == 0 ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    public CrossRef_AniDB_TMDB_Season? TmdbSeasonCrossReference =>
        TmdbEpisode is { } tmdbEpisode
            ? new(AnidbAnimeID, tmdbEpisode.TmdbSeasonID, TmdbShowID, tmdbEpisode.SeasonNumber)
            : null;

    public TMDB_Season? TmdbSeason =>
        TmdbEpisode?.TmdbSeason;

    public TMDB_Show? TmdbShow =>
        TmdbShowID == 0 ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithImages Implementation

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool primaryImage = false)
        => Utils.ServiceContainer.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, primaryImage);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null)
        => Utils.ServiceContainer.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired);

    #endregion

    #region ITmdbEpisodeCrossReference Implementation

    IShokoSeries? ITmdbEpisodeCrossReference.ShokoSeries => AnimeSeries;

    IShokoEpisode? ITmdbEpisodeCrossReference.ShokoEpisode => AnimeEpisode;

    ITmdbShow? ITmdbEpisodeCrossReference.TmdbShow => TmdbShow;

    ITmdbEpisode? ITmdbEpisodeCrossReference.TmdbEpisode => TmdbEpisode;

    #endregion
}
