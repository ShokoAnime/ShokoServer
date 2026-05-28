using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.API.v3.Models.ImageManagement;

/// <summary>
///   Image cross-reference DTO. Represents the association between an image
///   and an entity.
/// </summary>
public class ImageCrossReference
{
    /// <summary>
    ///   The local cross-reference identifier for administrative purposes.
    /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    ///   The universally/globally unique identifier (UUID/GUID) for the image.
    /// </summary>
    [Required]
    public Guid ImageID { get; set; }

    /// <summary>
    ///   The ID of the primary image for the associated entity.
    /// </summary>
    [Required]
    public Guid PrimaryImageID { get; set; }

    /// <summary>
    ///   The image type for this cross-reference (e.g. Primary, Backdrop,
    ///   Banner).
    /// </summary>
    [Required]
    public Image.ImageType ImageType { get; set; }

    /// <summary>
    ///   The image source.
    /// </summary>
    [Required]
    public DataSource ImageSource { get; set; }

    /// <summary>
    ///   The entity ID. This is the stringified identifier of the linked
    ///   entity.
    /// </summary>
    [Required]
    public string EntityID { get; set; }

    /// <summary>
    ///   The metadata entity type.
    /// </summary>
    [Required]
    public DataEntityType EntityType { get; set; }

    /// <summary>
    ///   The metadata entity source.
    /// </summary>
    [Required]
    public DataSource EntitySource { get; set; }

    /// <summary>
    ///   The season number if the linked entity is a season. If the linked
    ///   entity is an episode, this returns the episode's season number.
    /// </summary>
    public int? EntitySeasonNumber { get; set; }

    /// <summary>
    ///   The episode number if the linked entity is an episode.
    /// </summary>
    public int? EntityEpisodeNumber { get; set; }

    /// <summary>
    ///   The date when the entity was released.
    /// </summary>
    public DateOnly? EntityReleasedAt { get; set; }

    /// <summary>
    ///   The ordering number for where the image should appear in the list of
    ///   images for the entity and image type. Lower values appear first.
    /// </summary>
    [Required]
    public int Ordering { get; set; }

    /// <summary>
    ///   Indicates whether the image is enabled for the entity.
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; }

    /// <summary>
    ///   Indicates whether auto-download of the image is enabled.
    /// </summary>
    [Required]
    public bool IsDesired { get; set; }

    /// <summary>
    ///   Indicates whether this image is the preferred image for the
    ///   entity+type combination.
    /// </summary>
    [Required]
    public bool IsPreferred { get; set; }

    /// <summary>
    ///   Community rating for the image, if available.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Rating? CommunityRating { get; set; }

    /// <summary>
    ///   The source of the cross-reference.
    /// </summary>
    [Required]
    public DataSource Source { get; set; }

    /// <summary>
    ///   When the cross-reference was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the cross-reference was last updated.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    ///   The image associated with this cross-reference, if requested.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageSlim? Image { get; set; }

    public ImageCrossReference(IImageCrossReference xref, bool includeImage = false)
    {
        ID = xref.ID;
        ImageID = xref.ImageID;
        PrimaryImageID = xref.PrimaryImageID;
        ImageType = xref.ImageType.ToV3Dto();
        ImageSource = xref.ImageSource;
        EntityID = xref.EntityID;
        EntityType = xref.EntityType;
        EntitySource = xref.EntitySource;
        EntitySeasonNumber = xref.EntitySeasonNumber;
        EntityEpisodeNumber = xref.EntityEpisodeNumber;
        EntityReleasedAt = xref.EntityReleasedAt;
        Ordering = xref.Ordering;
        IsEnabled = xref.IsEnabled;
        IsDesired = xref.IsDesired;
        IsPreferred = xref.IsPreferred;
        if (xref.HasRating)
            CommunityRating = new()
            {
                Value = xref.Rating.Value,
                Votes = xref.RatingVotes.Value,
                MaxValue = 10,
                Type = "User",
                Source = xref.Source.ToString(),
            };
        Source = xref.Source;
        CreatedAt = xref.CreatedAt;
        LastUpdatedAt = xref.LastUpdatedAt;
        if (includeImage)
            Image = new ImageSlim(xref.GetImage()!);
    }
}
