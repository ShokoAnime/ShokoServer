using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Shoko;

/// <summary>
/// Unified image-to-entity join table. This model represents the association between
/// an image and an entity (series, group, episode, video). It stores the image type
/// for this association, preferred status, ordering, and other metadata.
/// </summary>
public partial class ShokoImage_Entity : IImageCrossReference
{
    internal const int SeasonIdHexLength = 24;

    [GeneratedRegex(@"^(?:[0-9]{1,23}|[a-f0-9]{24})$")]
    private static partial Regex SeasonIdRegex();

    #region Properties

    /// <inheritdoc/>
    public int ID { get; set; }

    /// <inheritdoc/>
    public Guid ImageID { get; set; }

    /// <inheritdoc/>
    public Guid PrimaryImageID { get; set; }

    /// <inheritdoc/>
    public ImageEntityType ImageType { get; set; }

    /// <inheritdoc/>
    public DataSource ImageSource { get; set; }

    /// <inheritdoc/>
    public DataSource EntitySource { get; set; }

    /// <inheritdoc/>
    public DataEntityType EntityType { get; set; }

    /// <inheritdoc/>
    public string EntityID { get; set; } = string.Empty;

    /// <inheritdoc/>
    public int? EntitySeasonNumber { get; set; }

    /// <inheritdoc/>
    public int? EntityEpisodeNumber { get; set; }

    /// <inheritdoc/>
    public DateOnly? EntityReleasedAt { get; set; }

    /// <inheritdoc/>
    public bool IsDesired { get; set; }

    /// <inheritdoc/>
    public bool IsPreferred { get; set; }

    /// <inheritdoc/>
    public bool IsEnabled { get; set; }

    /// <inheritdoc/>
    public uint Ordering { get; set; }

    /// <inheritdoc/>
    public double? Rating { get; set; }

    /// <inheritdoc/>
    public uint? RatingVotes { get; set; }

    /// <inheritdoc/>
    public DataSource Source { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    [Obsolete("Only for NHibernate. DO NOT USE ELSEWHERE.")]
    public ShokoImage_Entity() { }

    public ShokoImage_Entity(IImage image, IWithImages entity, ImageCrossReferenceData data, uint xrefsCount)
    {
        var imageManager = Utils.ServiceContainer.GetRequiredService<IImageManager>();
        if (!imageManager.TryGetMetadataForEntity(
            entity,
            out var entitySource,
            out var entityType,
            out var entityID,
            out var entitySeasonNumber,
            out var entityEpisodeNumber,
            out var releasedAt
        ))
            throw new ArgumentException(nameof(entity), "Invalid entity given to constructor");

        ImageID = image.ID;
        ImageSource = image.Source;

        EntitySource = entitySource;
        EntityType = entityType;
        EntityID = entityID;
        EntitySeasonNumber = entitySeasonNumber;
        EntityEpisodeNumber = entityEpisodeNumber;
        EntityReleasedAt = releasedAt;

        ImageType = data.ImageType;
        Source = data.Source;
        IsEnabled = data.IsEnabled;
        IsDesired = data.IsDesired;
        IsPreferred = data.IsPreferred;
        Ordering = data.Ordering ?? xrefsCount;
        Rating = data.Rating;
        RatingVotes = data.RatingVotes;

        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    public bool Update(ImageCrossReferenceUpdateData? data, IWithImages? entity)
    {
        var updated = false;
        if (data is not null)
        {
            if (data.IsEnabled.HasValue && IsEnabled != data.IsEnabled.Value)
            {
                IsEnabled = data.IsEnabled.Value;
                updated = true;
            }

            if (data.IsDesired.HasValue && IsDesired != data.IsDesired.Value)
            {
                IsDesired = data.IsDesired.Value;
                updated = true;
            }

            if (data.IsPreferred.HasValue && IsPreferred != data.IsPreferred.Value)
            {
                IsPreferred = data.IsPreferred.Value;
                updated = true;
            }

            if (data.Ordering.HasValue && Ordering != data.Ordering.Value)
            {
                Ordering = data.Ordering.Value;
                updated = true;
            }

            if (data.HasRatingSet)
            {
                var newRating = data.HasRating ? data.Rating : null;
                var newRatingVotes = data.HasRating ? data.RatingVotes : null;
                if (Rating != newRating || RatingVotes != newRatingVotes)
                {
                    Rating = newRating;
                    RatingVotes = newRatingVotes;
                    LastUpdatedAt = DateTime.Now;
                    updated = true;
                }
            }
        }

        if (entity is not null)
        {
            var imageManager = Utils.ServiceContainer.GetRequiredService<IImageManager>();
            if (!imageManager.TryGetMetadataForEntity(
                entity,
                out var entitySource,
                out var entityType,
                out var entityID,
                out var entitySeasonNumber,
                out var entityEpisodeNumber,
                out var releasedAt
            ))
                throw new ArgumentException(nameof(entity), "Invalid entity given to Update method.");

            if (EntitySource != entitySource || EntityType != entityType || !string.Equals(EntityID, entityID))
                throw new ArgumentException(nameof(entity), "Different entity given to Update method.");

            if (EntitySeasonNumber != entitySeasonNumber)
            {
                EntitySeasonNumber = entitySeasonNumber;
                updated = true;
            }

            if (EntityEpisodeNumber != entityEpisodeNumber)
            {
                EntityEpisodeNumber = entityEpisodeNumber;
                updated = true;
            }

            if (EntityReleasedAt != releasedAt)
            {
                EntityReleasedAt = releasedAt;
                updated = true;
            }
        }

        if (updated)
            LastUpdatedAt = DateTime.Now;

        return updated;
    }

    /// <summary>
    /// The associated image record.
    /// </summary>
    public ShokoImage? GetImage() => RepoFactory.ShokoImage.GetByID(ImageID);

    public ShokoImage? GetPrimaryImage() => RepoFactory.ShokoImage.GetByID(PrimaryImageID);

    public IWithImages? GetEntity() => (EntitySource, EntityType) switch
    {
        // Shoko
        (DataSource.Shoko, DataEntityType.Group) => !int.TryParse(EntityID, out var shokoGroupID)
            ? null : RepoFactory.AnimeGroup.GetByID(shokoGroupID),

        (DataSource.Shoko, DataEntityType.Series) => !int.TryParse(EntityID, out var shokoSeriesID)
            ? null : RepoFactory.AnimeSeries.GetByID(shokoSeriesID),

        (DataSource.Shoko, DataEntityType.Season) =>
            EntityID.Split(':') is not { Length: 3 } parts ||
            !int.TryParse(parts[0], out var shokoSeriesID) ||
            RepoFactory.AnimeSeries.GetByID(shokoSeriesID) is not { } shokoSeries ||
            !Enum.TryParse<EpisodeType>(parts[1], true, out var episodeType) ||
            !int.TryParse(parts[2], out var seasonNumber)
                ? null : new AnimeSeason(shokoSeries, episodeType, seasonNumber),

        (DataSource.Shoko, DataEntityType.Episode) => !int.TryParse(EntityID, out var shokoEpisodeID)
            ? null : RepoFactory.AnimeEpisode.GetByID(shokoEpisodeID),

        (DataSource.Shoko, DataEntityType.Video) => !int.TryParse(EntityID, out var videoID)
            ? null : RepoFactory.VideoLocal.GetByID(videoID),

        (DataSource.Shoko, DataEntityType.User) => !int.TryParse(EntityID, out var userID)
            ? null : RepoFactory.JMMUser.GetByID(userID),
        // AniDB
        (DataSource.AniDB, DataEntityType.Anime) => !int.TryParse(EntityID, out var anidbAnimeID)
            ? null : RepoFactory.AniDB_Anime.GetByAnimeID(anidbAnimeID),

        (DataSource.AniDB, DataEntityType.Season) =>
            EntityID.Split(':') is not { Length: 3 } parts ||
            !int.TryParse(parts[0], out var anidbAnimeID) ||
            RepoFactory.AniDB_Anime.GetByAnimeID(anidbAnimeID) is not { } anidbAnime ||
            !Enum.TryParse<EpisodeType>(parts[1], true, out var episodeType) ||
            !int.TryParse(parts[2], out var seasonNumber)
                ? null : new AniDB_Season(anidbAnime, episodeType, seasonNumber),

        (DataSource.AniDB, DataEntityType.Episode) => !int.TryParse(EntityID, out var anidbEpisodeID)
            ? null : RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID),

        (DataSource.AniDB, DataEntityType.Studio) =>
            !int.TryParse(EntityID, out var anidbStudioID) ||
            RepoFactory.AniDB_Creator.GetByCreatorID(anidbStudioID) is not { } creator
                ? null : new AniDB_Studio(creator),

        (DataSource.AniDB, DataEntityType.Creator) => !int.TryParse(EntityID, out var anidbCreatorID)
            ? null : RepoFactory.AniDB_Creator.GetByCreatorID(anidbCreatorID),

        (DataSource.AniDB, DataEntityType.Character) => !int.TryParse(EntityID, out var anidbCharacterID)
            ? null : RepoFactory.AniDB_Character.GetByCharacterID(anidbCharacterID),

        // TMDB
        (DataSource.TMDB, DataEntityType.Collection) => !int.TryParse(EntityID, out var tmdbCollectionID)
            ? null : RepoFactory.TMDB_Collection.GetByTmdbCollectionID(tmdbCollectionID),

        (DataSource.TMDB, DataEntityType.Movie) => !int.TryParse(EntityID, out var tmdbMovieID)
            ? null : RepoFactory.TMDB_Movie.GetByTmdbMovieID(tmdbMovieID),

        (DataSource.TMDB, DataEntityType.Show) => !int.TryParse(EntityID, out var tmdbShowID)
            ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(tmdbShowID),

        (DataSource.TMDB, DataEntityType.Season) => !SeasonIdRegex().IsMatch(EntityID)
            ? null : EntityID is { Length: SeasonIdHexLength }
                ? RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(EntityID)
                : RepoFactory.TMDB_Season.GetByTmdbSeasonID(int.Parse(EntityID)),

        (DataSource.TMDB, DataEntityType.Episode) => !int.TryParse(EntityID, out var tmdbEpisodeID)
            ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(tmdbEpisodeID),

        (DataSource.TMDB, DataEntityType.Person) => !int.TryParse(EntityID, out var tmdbPersonID)
            ? null : RepoFactory.TMDB_Person.GetByTmdbPersonID(tmdbPersonID),

        (DataSource.TMDB, DataEntityType.Studio) => !int.TryParse(EntityID, out var tmdbCompanyID)
            ? null : RepoFactory.TMDB_Company.GetByTmdbCompanyID(tmdbCompanyID),

        (DataSource.TMDB, DataEntityType.Network) => !int.TryParse(EntityID, out var tmdbNetworkID)
            ? null : RepoFactory.TMDB_Network.GetByTmdbNetworkID(tmdbNetworkID),

        // Default
        _ => null,
    };

    #endregion

    #region IImageCrossReference Implementation

    /// <inheritdoc/>
    IImage? IImageCrossReference.GetImage() => GetImage();

    /// <inheritdoc/>
    IImage? IImageCrossReference.GetPrimaryImage() => GetPrimaryImage();

    #endregion
}
