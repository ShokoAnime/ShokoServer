using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.UserData.Enums;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class EpisodeUserDataSavedSignalRModel(EpisodeUserDataSavedEventArgs args)
{
    /// <summary>
    /// The ID of the user which had their data updated.
    /// </summary>
    public int UserID { get; } = args.UserData.UserID;

    /// <summary>
    /// The ID of the episode which had its user data updated.
    /// </summary>
    public int EpisodeID { get; } = args.UserData.EpisodeID;

    /// <summary>
    /// The ID of the series which had its user data updated.
    /// </summary>
    public int SeriesID { get; } = args.UserData.SeriesID;

    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public EpisodeUserDataSaveReason Reason { get; } = args.Reason;

    /// <summary>
    /// Indicates that the user data was imported from another source.
    /// </summary>
    public bool IsImport { get; } = args.IsImport;

    /// <summary>
    /// The source if the <see cref="Reason"/> has the
    /// <see cref="EpisodeUserDataSaveReason.Import">Import flag</see> set.
    /// </summary>
    public string? ImportSource { get; } = args.ImportSource;

    #region Episode Data

    /// <summary>
    ///   Gets the number of times the episode has been played for the user,
    ///   locally or otherwise.
    /// </summary>
    public int PlaybackCount { get; } = args.UserData.PlaybackCount;

    /// <summary>
    ///   Gets the date and time when the episode was last played to completion,
    ///   locally or otherwise, by the user.
    /// </summary>
    public DateTime? LastPlayedAt { get; } = args.UserData.LastPlayedAt?.ToUniversalTime();

    /// <summary>
    ///   Indicates that the user has marked the episode as favorite.
    /// </summary>
    public bool IsFavorite { get; } = args.UserData.IsFavorite;

    /// <summary>
    ///   The unique tags assigned to the episode by the user.
    /// </summary>
    public IReadOnlyList<string> UserTags { get; } = args.UserData.UserTags;

    /// <summary>
    ///   The user rating, on a scale of 1-10 with a maximum of 1 decimal places, or <c>null</c> if unrated.
    /// </summary>
    public double? UserRating { get; } = args.UserData.UserRating;

    #endregion

    #region Video Data

    /// <summary>
    ///   Gets the date and time when a video linked to the episode was last
    ///   played to completion.
    /// </summary>
    public DateTime? LastVideoPlayedAt { get; } = args.UserData.LastVideoPlayedAt?.ToUniversalTime();

    /// <summary>
    ///   Gets the date and time when the user data for a video linked to the
    ///   episode was last updated, regardless of if it was watched to
    ///   completion or not.
    /// </summary>
    public DateTime? LastVideoUpdatedAt { get; } = args.UserData.LastVideoUpdatedAt?.ToUniversalTime();

    #endregion

    /// <summary>
    ///   Indicates that the episode has been watched to completion at least
    ///   once by the user, be it locally or otherwise.
    /// </summary>
    public bool IsWatched { get; } = args.UserData.LastPlayedAt.HasValue || args.UserData.PlaybackCount > 0;

    /// <summary>
    ///   Indicates that the user has rated the episode.
    /// </summary>
    public bool HasUserRating { get; } = args.UserData.UserRating.HasValue;

    /// <summary>
    /// Gets the date and time when the user data was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; } = args.UserData.LastUpdatedAt.ToUniversalTime();
}
