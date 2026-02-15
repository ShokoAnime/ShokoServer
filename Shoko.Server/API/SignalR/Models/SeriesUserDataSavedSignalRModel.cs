using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.UserData.Enums;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class SeriesUserDataSavedSignalRModel(SeriesUserDataSavedEventArgs args)
{
    /// <summary>
    /// The ID of the user which had their data updated.
    /// </summary>
    public int UserID { get; } = args.UserData.UserID;

    /// <summary>
    /// The ID of the series which had its user data updated.
    /// </summary>
    public int SeriesID { get; } = args.UserData.SeriesID;

    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public SeriesUserDataSaveReason Reason { get; } = args.Reason;

    /// <summary>
    /// Indicates that the user data was imported from another source.
    /// </summary>
    public bool IsImport { get; } = args.IsImport;

    /// <summary>
    /// The source if the <see cref="Reason"/> has the
    /// <see cref="SeriesUserDataSaveReason.Import">Import flag</see> set.
    /// </summary>
    public string? ImportSource { get; } = args.ImportSource;

    #region Series Data

    /// <summary>
    ///   Indicates that the user has marked the series as favorite.
    /// </summary>
    public bool IsFavorite { get; } = args.UserData.IsFavorite;

    /// <summary>
    ///   The unique tags assigned to the series by the user.
    /// </summary>
    public IReadOnlyList<string> UserTags { get; } = args.UserData.UserTags;

    /// <summary>
    ///   The user rating, on a scale of 1-10 with a maximum of 1 decimal
    ///   places, or <c>null</c> if unrated.
    /// </summary>
    public double? UserRating { get; }

    /// <summary>
    ///   The user rating vote type.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public SeriesVoteType? UserRatingVoteType { get; } = args.UserData.UserRatingVoteType;

    #endregion

    #region Episode Data

    /// <summary>
    ///   Gets the date and time when an episode linked to the series was last
    ///   played to completion.
    /// </summary>
    public DateTime? LastEpisodePlayedAt { get; } = args.UserData.LastEpisodePlayedAt;

    /// <summary>
    ///   The number of normal episodes or specials that have not been watched
    ///   to completion, and are not hidden.
    /// </summary>
    public int UnwatchedEpisodeCount { get; } = args.UserData.UnwatchedEpisodeCount;

    /// <summary>
    ///   The number of normal episodes or specials that have not been watched
    ///   to completion, and are hidden.
    /// </summary>
    public int HiddenUnwatchedEpisodeCount { get; } = args.UserData.HiddenUnwatchedEpisodeCount;

    /// <summary>
    ///   The number of normal episodes or specials that have been watched to
    ///   completion.
    /// </summary>
    public int WatchedEpisodeCount { get; } = args.UserData.WatchedEpisodeCount;

    #endregion

    #region Video Data

    /// <summary>
    ///   Gets the number of times any videos linked to the series has been
    ///   played to completion.
    /// </summary>
    public int VideoPlaybackCount { get; } = args.UserData.VideoPlaybackCount;

    /// <summary>
    ///   Gets the date and time when a video linked to the series was last
    ///   played to completion.
    /// </summary>
    public DateTime? LastVideoPlayedAt { get; } = args.UserData.LastVideoPlayedAt?.ToUniversalTime();

    /// <summary>
    ///   Gets the date and time when the user data for a video linked to the
    ///   series was last updated, regardless of if it was watched to completion
    ///   or not. Can be used to determine continue watching and next-up order
    ///   for the series, etc..
    /// </summary>
    public DateTime? LastVideoUpdatedAt { get; } = args.UserData.LastVideoUpdatedAt?.ToUniversalTime();

    #endregion

    /// <summary>
    ///   Indicates that the series has been watched to completion at least
    ///   once by the user, be it locally or otherwise.
    /// </summary>
    public bool IsWatched { get; } = args.UserData.UnwatchedEpisodeCount is 0;

    /// <summary>
    ///   Indicates that the user has rated the series.
    /// </summary>
    public bool HasUserRating { get; } = args.UserData.UserRating.HasValue && args.UserData.UserRatingVoteType.HasValue;

    /// <summary>
    /// Gets the date and time when the user data was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; } = args.UserData.LastUpdatedAt.ToUniversalTime();
}
