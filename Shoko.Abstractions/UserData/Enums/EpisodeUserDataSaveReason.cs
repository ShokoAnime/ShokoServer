using System;

namespace Shoko.Abstractions.UserData.Enums;

/// <summary>
///   The reason the user data for the episode and user is being saved.
/// </summary>
[Flags]
public enum EpisodeUserDataSaveReason
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
    /// The user data is being saved because the playback date for the episode changed.
    /// </summary>
    LastPlayedAt = 1 << 1,

    /// <summary>
    /// The user data is being saved because the playback count for the episode changed.
    /// </summary>
    PlaybackCount = 1 << 2,

    /// <summary>
    /// The user data is being saved because the user toggled their favorite status for the episode.
    /// </summary>
    IsFavorite = 1 << 3,

    /// <summary>
    /// The user data is being saved because the user updated their unique tags for the episode.
    /// </summary>
    UserTags = 1 << 4,

    /// <summary>
    /// The user data is being saved because the user rated the episode.
    /// </summary>
    UserRating = 1 << 5,
}
