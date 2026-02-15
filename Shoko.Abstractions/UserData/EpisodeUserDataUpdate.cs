using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.UserData;

/// <summary>
///   Represents an update to the user-specific data associated with a episode.
/// </summary>
public class EpisodeUserDataUpdate
{
    /// <summary>
    ///   Override or set the number of times the episode has been played.
    /// </summary>
    public int? PlaybackCount { get; set; }

    /// <summary>
    ///   Indicates that the user has set or unset the last played date for the episode.
    /// </summary>
    public bool HasLastPlayedAt { get; private set; }

    private DateTime? _lastPlayedAt;

    /// <summary>
    ///   Override the last date and time the episode was played to completion,
    ///   locally or otherwise, by the user.
    /// </summary>
    public DateTime? LastPlayedAt
    {
        get => _lastPlayedAt;
        set
        {
            HasLastPlayedAt = value.HasValue;
            _lastPlayedAt = value;
        }
    }

    /// <summary>
    ///   Override the favorite status of the episode by the user.
    /// </summary>
    public bool? IsFavorite { get; set; }

    /// <summary>
    ///   Override the unique tags assigned to the episode by the user.
    /// </summary>
    public IEnumerable<string>? UserTags { get; set; }

    /// <summary>
    ///   Indicates that the user has data update has a valid user rating set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(UserRating))]
    public bool HasUserRating => _userRating.HasValue;

    private double? _userRating;

    /// <summary>
    ///   Indicates that the user has set or unset the user rating for the episode.
    /// </summary>
    public bool HasSetUserRating { get; private set; }

    /// <summary>
    ///   Override the user rating for the episode. Set on a scale of 1-10 with 1 decimal places, or <c>-1</c> or <c>null</c> to unset the rating. All other values are invalid and will throw an exception.
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
        }
    }

    /// <summary>
    ///   Override when the data was last updated. If not set, then the current
    ///   time will be used.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }
}
