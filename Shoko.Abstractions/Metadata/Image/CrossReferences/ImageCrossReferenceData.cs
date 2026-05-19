using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image.CrossReferences;

/// <summary>
///   Data transfer object (DTO) for creating a cross-reference between an image
///   and an entity.
/// </summary>
public sealed class ImageCrossReferenceData
{
    /// <summary>
    /// The image type for the association (poster, backdrop, banner, etc.).
    /// </summary>
    public required ImageEntityType ImageType { get; set; }

    /// <summary>
    /// Source of the cross-reference. Defaults to <see cref="DataSource.User"/>.
    /// </summary>
    public DataSource Source { get; set; } = DataSource.User;

    /// <summary>
    ///   Whether the image is enabled for the entity. Defaults to <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///   Whether the image should be auto-downloaded. Defaults to <c>false</c>.
    /// </summary>
    public bool IsDesired { get; set; } = false;

    /// <summary>
    ///   Whether the is the preferred image for the entity+type. Defaults to
    ///   <c>false</c>.
    /// </summary>
    public bool IsPreferred { get; set; } = false;

    private int? _ordering;

    /// <summary>
    ///   Sort order index for images of the same type. If set to <c>null</c>,
    ///   will be appended at the end.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the ordering is set to a value less than 0.
    /// </exception>
    public int? Ordering
    {
        get => _ordering;
        set
        {
            if (value is not null and < 0)
                throw new ArgumentOutOfRangeException(nameof(Ordering), "Ordering must be greater than or equal to 0 if set.");
            _ordering = value;
        }
    }

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
            _ratingVotes = value;
            if (value.HasValue && !_rating.HasValue)
                _rating = 1;
            else if (!value.HasValue && _rating.HasValue)
                _rating = null;
        }
    }
}
