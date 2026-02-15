using System;

namespace Shoko.Abstractions.UserData.Enums;

/// <summary>
///   The reason the user data for the series and user is being saved.
/// </summary>
[Flags]
public enum SeriesUserDataSaveReason
{
    /// <summary>
    /// The user data is being saved for no specific reason.
    /// </summary>
    None = 0,

    /// <summary>
    /// The user data is being imported from another source.
    /// </summary>
    Import = 1 << 0,

    /// <summary>
    /// The user data is being saved because the user stats for the series and user changed.
    /// </summary>
    SeriesStats = 1 << 1,

    /// <summary>
    /// The user data is being saved because the user toggled their favorite status for the series.
    /// </summary>
    IsFavorite = 1 << 2,

    /// <summary>
    /// The user data is being saved because the user updated their unique tags for the series.
    /// </summary>
    UserTags = 1 << 3,

    /// <summary>
    /// The user data is being saved because the user rated the series.
    /// </summary>
    UserRating = 1 << 4,
}
