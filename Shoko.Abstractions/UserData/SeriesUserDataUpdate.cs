using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.UserData.Enums;

namespace Shoko.Abstractions.UserData;

/// <summary>
///   Represents an update to the user-specific data associated with a series.
/// </summary>
public class SeriesUserDataUpdate
{
    /// <summary>
    ///   Override the favorite status of the series by the user.
    /// </summary>
    public bool? IsFavorite { get; set; }

    /// <summary>
    ///   Override the unique tags assigned to the series by the user.
    /// </summary>
    public IEnumerable<string>? UserTags { get; set; }

    /// <summary>
    ///   Indicates that the user has data update has a valid user rating set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(UserRating), nameof(UserRatingVoteType))]
    public bool HasUserRating => _userRating.HasValue && _userRatingVoteType.HasValue;

    /// <summary>
    ///   Indicates that the user has set or unset the user rating for the series.
    /// </summary>
    public bool HasSetUserRating { get; private set; }

    private double? _userRating;

    /// <summary>
    ///   Override the user rating for the series. Set on a scale of 1-10 with 1 decimal places, or <c>-1</c> or <c>null</c> to unset the rating. All other values are invalid and will throw an exception.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="value"/> is not between <c>100</c> and <c>1000</c>, or <c>-1</c> or <c>null</c> for no rating.
    /// </exception>
    public double? UserRating
    {
        get => _userRating;
        set
        {
            if (value is -1)
                value = null;
            if (value.HasValue)
                value = Math.Round(value.Value, 1, MidpointRounding.AwayFromZero);
            if (value is not null && (value < 1 || value > 10))
                throw new ArgumentOutOfRangeException(nameof(UserRating), "User rating must be set between 1 and 10, or set to -1 or null to unset the rating.");

            HasSetUserRating = true;
            _userRating = value;
            if (!_userRating.HasValue)
                _userRatingVoteType = null;
            else if (!_userRatingVoteType.HasValue)
                _userRatingVoteType = SeriesVoteType.Temporary;
        }
    }

    private SeriesVoteType? _userRatingVoteType;

    /// <summary>
    ///   Override the user rating vote type.
    /// </summary>
    public SeriesVoteType? UserRatingVoteType
    {
        get => _userRatingVoteType;
        set
        {
            HasSetUserRating = true;
            _userRatingVoteType = value;
            if (!_userRatingVoteType.HasValue)
                _userRating = null;
            else if (!UserRating.HasValue)
                _userRating = 1;
        }
    }
}
