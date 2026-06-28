using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Show : ITmdbShowCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_ShowID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Show() { }

    public CrossRef_AniDB_TMDB_Show(int anidbAnimeId, int tmdbShowId, MatchRating matchRating = MatchRating.UserVerified)
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbShowID = tmdbShowId;
        MatchRating = matchRating;
    }

    #endregion
    #region Methods

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    #endregion

    #region IWithImages Implementation

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool? isAvailable = null, bool primaryImage = false, bool? linkedEntityImages = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, isAvailable: isAvailable, primaryImage: primaryImage, linkedEntityImages: linkedEntityImages);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool? isAvailable = null, bool? primaryImage = null, bool? linkedEntityImages = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, isAvailable, primaryImage, linkedEntityImages);

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region ITmdbShowCrossReference Implementation

    IShokoSeries? ITmdbShowCrossReference.ShokoSeries => AnimeSeries;

    ITmdbShow? ITmdbShowCrossReference.TmdbShow => TmdbShow;

    #endregion
}
