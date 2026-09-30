using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Shoko;

/// <summary>
/// Unified image-to-entity join table. This model represents the association between
/// an image and an entity (series, group, episode, video). It stores the image type
/// for this association, preferred status, ordering, and other metadata.
/// </summary>
public class ShokoImage_Entity : IImageCrossReference
{
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
    public MetadataSource ImageSource { get; set; } = null!;

    /// <summary>
    ///   The source of the linked entity, the first part of its ID.
    /// </summary>
    public MetadataSource EntitySource { get; set; } = null!;

    /// <summary>
    ///   The kind of the linked entity, the second part of its ID.
    /// </summary>
    public MetadataEntityType EntityType { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the linked entity, the last part of its ID.
    /// </summary>
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
    public bool IsAvailable => GetImage()?.IsAvailable ?? false;

    /// <inheritdoc/>
    public bool IsPrimaryAvailable => GetPrimaryImage()?.IsAvailable ?? false;

    /// <inheritdoc/>
    public int Ordering { get; set; }

    /// <inheritdoc/>
    public double? Rating { get; set; }

    /// <inheritdoc/>
    public int? RatingVotes { get; set; }

    /// <inheritdoc/>
    public MetadataSource Source { get; set; } = null!;

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    [Obsolete("Only for NHibernate. DO NOT USE ELSEWHERE.")]
    public ShokoImage_Entity() { }

    public ShokoImage_Entity(IImage image, IWithImages entity, ImageCrossReferenceData data, int xrefsCount)
    {
        var entityID = entity.ID;
        var (entitySeasonNumber, entityEpisodeNumber, releasedAt) = GetEntityDetails(entity);

        ImageID = image.ID;
        PrimaryImageID = image.PrimaryID;
        ImageSource = image.Source;

        EntitySource = entityID.Source;
        EntityType = entityID.EntityType;
        EntityID = entityID.ID;
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
                    updated = true;
                }
            }
        }

        if (entity is not null)
        {
            if (entity.ID != GetEntityID())
                throw new ArgumentException("Different entity given to Update method.", nameof(entity));

            var (entitySeasonNumber, entityEpisodeNumber, releasedAt) = GetEntityDetails(entity);

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

    /// <summary>
    ///   The ID of the linked entity, built from <see cref="EntitySource"/>,
    ///   <see cref="EntityType"/> and <see cref="EntityID"/>.
    /// </summary>
    public MetadataGuid GetEntityID() => new(EntitySource, EntityType, EntityID);

    public IWithImages? GetEntity() =>
        ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetEntityForImage(GetEntityID());

    /// <summary>
    ///   What a cross-reference records about its entity beside the ID: the
    ///   season and episode numbers of a season or an episode, and the date
    ///   the entity was released or born, when it has one.
    /// </summary>
    /// <param name="entity">The linked entity.</param>
    /// <returns>The season number, the episode number and the release date.</returns>
    internal static (int? SeasonNumber, int? EpisodeNumber, DateOnly? ReleasedAt) GetEntityDetails(IWithImages entity)
        => entity switch
        {
            ICollection or ICharacter or IStudio or INetwork or IAiringChannel or IUser => (null, null, null),
            IMetadataCrossReference => (null, null, null),
            IMovie movie => (null, null, movie.ReleaseDate?.ToDateOnly()),
            ISeries series => (null, null, series.AirDate?.IsComplete ?? false ? series.AirDate.Value.ToDateOnly() : null),
            ISeason season => (season.SeasonNumber, null, season.Episodes.Select(episode => episode.AirDate).WhereNotNull().Order().FirstOrDefault()),
            IEpisode episode => (episode.SeasonNumber, episode.EpisodeNumber, episode.AirDate),
            IVideo video => (null, null, video.ReleaseInfo?.ReleasedAt),
            ICreator creator => (null, null, creator.BirthDay is { } birthDay && birthDay.TryGetDateOnly(out var bornAt) ? bornAt : null),
            _ => (null, null, null),
        };

    #endregion

    #region IImageCrossReference Implementation

    /// <inheritdoc/>
    MetadataGuid IImageCrossReference.EntityID => GetEntityID();

    /// <inheritdoc/>
    IImage? IImageCrossReference.GetImage() => GetImage();

    /// <inheritdoc/>
    IImage? IImageCrossReference.GetPrimaryImage() => GetPrimaryImage();

    #endregion
}
