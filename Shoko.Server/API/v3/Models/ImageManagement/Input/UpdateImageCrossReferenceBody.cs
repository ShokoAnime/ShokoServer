using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

#nullable enable
namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for updating an existing image cross-reference. Tracks which
///   properties were explicitly set to support partial updates.
/// </summary>
public class UpdateImageCrossReferenceBody
{
    private readonly HashSet<string> _setProperties = [];

    private int? _ordering;
    private double? _rating;
    private int? _ratingVotes;

    /// <summary>
    ///   Whether the image is enabled for the entity.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    ///   Whether the image should be auto-downloaded.
    /// </summary>
    public bool? IsDesired { get; set; }

    /// <summary>
    ///   Whether this is the preferred image for the entity+type.
    /// </summary>
    public bool? IsPreferred { get; set; }

    /// <summary>
    ///   Sort order index for images of the same type.
    /// </summary>
    public int? Ordering
    {
        get => _ordering;
        set
        {
            _setProperties.Add(nameof(Ordering));
            _ordering = value;
        }
    }

    /// <summary>
    ///   Community rating normalized on a scale of 1-10.
    /// </summary>
    public double? Rating
    {
        get => _rating;
        set
        {
            _setProperties.Add(nameof(Rating));
            _rating = value;
        }
    }

    /// <summary>
    ///   Number of community votes for the rating.
    /// </summary>
    public int? RatingVotes
    {
        get => _ratingVotes;
        set
        {
            _setProperties.Add(nameof(RatingVotes));
            _ratingVotes = value;
        }
    }

    public bool HasOrderingSet => _setProperties.Contains(nameof(Ordering));
    public bool HasRatingSet => _setProperties.Contains(nameof(Rating)) || _setProperties.Contains(nameof(RatingVotes));

    public ImageCrossReferenceUpdateData ToImageCrossReferenceUpdateData()
    {
        var data = new ImageCrossReferenceUpdateData
        {
            IsEnabled = IsEnabled,
            IsDesired = IsDesired,
            IsPreferred = IsPreferred,
        };
        if (HasOrderingSet)
            data.Ordering = Ordering;
        if (HasRatingSet)
        {
            data.Rating = Rating;
            data.RatingVotes = RatingVotes;
        }
        return data;
    }
}
