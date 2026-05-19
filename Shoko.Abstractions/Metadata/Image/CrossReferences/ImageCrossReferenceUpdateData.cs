using System;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Metadata.Image.CrossReferences;

/// <summary>
///   Data transfer object (DTO) for updating an existing image cross-reference
///   between an image and an entity with support for partial updates.
/// </summary>
public sealed class ImageCrossReferenceUpdateData
{
    /// <summary>
    ///   Whether the image is enabled for the entity. Defaults to <c>true</c>.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    ///   Whether the image should be auto-downloaded. Defaults to <c>false</c>.
    /// </summary>
    public bool? IsDesired { get; set; }

    /// <summary>
    ///   Whether the is the preferred image for the entity+type. Defaults to
    ///   <c>false</c>.
    /// </summary>
    public bool? IsPreferred { get; set; }

    /// <summary>
    ///   Sort order index for images of the same type. If set to <c>null</c>,
    ///   will be appended at the end.
    /// </summary>
    public uint? Ordering { get; set; }

    /// <summary>
    ///   Used by the manager to determine if a rating should be updated. This
    ///   is set to <c>true</c> when a rating related properties are set.
    /// </summary>
    public bool HasRatingSet { get; private set; }

    /// <summary>
    ///   Indicates that the cross-reference has a rating set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rating), nameof(RatingVotes))]
    public bool HasRating { get => _rating.HasValue && _ratingVotes.HasValue; }

    private double? _rating;

    /// <summary>
    ///   Community rating normalized on a scale of 1-10. Must be between 1 and
    ///   10 if set, or <c>null</c> to unset.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the rating is set to a value outside the range of 1-10.
    /// </exception>
    public double? Rating
    {
        get => _rating;
        set
        {
            if (value is not null and not (>= 1 and <= 10))
                throw new ArgumentOutOfRangeException(nameof(Rating), "Rating must be normalized between 1 and 10 if set.");
            HasRatingSet = true;
            _rating = value;
            if (value.HasValue && !_ratingVotes.HasValue)
                _ratingVotes = 0;
            else if (!value.HasValue && _ratingVotes.HasValue)
                _ratingVotes = null;
        }
    }

    private int? _ratingVotes;

    /// <summary>
    ///   Number of community votes for the rating. Must be non-negative if set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the rating votes are set to a negative value.
    /// </exception>
    public int? RatingVotes
    {
        get => _ratingVotes;
        set
        {
            if (value is not null and < 0)
                throw new ArgumentOutOfRangeException(nameof(RatingVotes), "Rating votes must be non-negative if set.");
            HasRatingSet = true;
            _ratingVotes = value;
            if (value.HasValue && !_rating.HasValue)
                _rating = 1;
            else if (!value.HasValue && _rating.HasValue)
                _rating = null;
        }
    }
}
