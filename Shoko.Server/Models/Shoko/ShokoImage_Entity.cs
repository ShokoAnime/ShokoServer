using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
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
    public DataSource Source { get; set; }

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
        var imageManager = ISystemService.StaticServices.GetRequiredService<IImageManager>();
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
        PrimaryImageID = image.PrimaryID;
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
                    updated = true;
                }
            }
        }

        if (entity is not null)
        {
            var imageManager = ISystemService.StaticServices.GetRequiredService<IImageManager>();
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

    public IWithImages? GetEntity() =>
        ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetEntityForImage(EntitySource, EntityType, EntityID);

    #endregion

    #region IImageCrossReference Implementation

    /// <inheritdoc/>
    IImage? IImageCrossReference.GetImage() => GetImage();

    /// <inheritdoc/>
    IImage? IImageCrossReference.GetPrimaryImage() => GetPrimaryImage();

    #endregion
}
