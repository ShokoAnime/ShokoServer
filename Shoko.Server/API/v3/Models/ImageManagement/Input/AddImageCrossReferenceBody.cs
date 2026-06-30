using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for adding a cross-reference between an image and an entity.
/// </summary>
public class AddImageCrossReferenceBody
{
    /// <summary>
    ///   The UUID of the image to link.
    /// </summary>
    [Required]
    public Guid ImageID { get; set; }

    /// <summary>
    ///   The image type for the association (Primary, Backdrop, Banner,
    ///   etc.).
    /// </summary>
    [Required]
    public ImageEntityType ImageType { get; set; }

    /// <summary>
    ///   Source of the cross-reference. Defaults to User.
    /// </summary>
    public DataSource Source { get; set; } = DataSource.User;

    /// <summary>
    ///   Whether the image is enabled for the entity.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///   Whether the image should be auto-downloaded.
    /// </summary>
    public bool IsDesired { get; set; } = false;

    /// <summary>
    ///   Whether this is the preferred image for the entity+type.
    /// </summary>
    public bool IsPreferred { get; set; } = false;

    /// <summary>
    ///   Sort order index for images of the same type.
    /// </summary>
    public int? Ordering { get; set; }

    /// <summary>
    ///   Community rating normalized on a scale of 1-10.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    ///   Number of community votes for the rating.
    /// </summary>
    public int? RatingVotes { get; set; }

    public ImageCrossReferenceData ToImageCrossReferenceData()
    {
        var data = new ImageCrossReferenceData
        {
            ImageType = ImageType,
            Source = Source,
            IsEnabled = IsEnabled,
            IsDesired = IsDesired,
            IsPreferred = IsPreferred,
            Ordering = Ordering,
        };
        if (Rating.HasValue || RatingVotes.HasValue)
        {
            data.Rating = Rating;
            data.RatingVotes = RatingVotes;
        }
        return data;
    }
}
