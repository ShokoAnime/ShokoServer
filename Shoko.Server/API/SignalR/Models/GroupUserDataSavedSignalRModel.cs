using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.User.Events;

namespace Shoko.Server.API.SignalR.Models;

public class GroupUserDataSavedSignalRModel(GroupUserDataSavedEventArgs args)
{
    /// <summary>
    /// The ID of the user which had their data updated.
    /// </summary>
    public int UserID { get; } = args.UserData.UserID;

    /// <summary>
    /// The ID of the group which had its user data updated.
    /// </summary>
    public int GroupID { get; } = args.UserData.GroupID;

    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public GroupUserDataSaveReason Reason { get; } = args.Reason;

    /// <summary>
    /// Indicates that the user data was imported from another source.
    /// </summary>
    public bool IsImport { get; } = args.IsImport;

    /// <summary>
    /// The source if the <see cref="Reason"/> has the
    /// <see cref="GroupUserDataSaveReason.Import">Import flag</see> set.
    /// </summary>
    public string? ImportSource { get; } = args.ImportSource;

    #region User Data

    /// <summary>
    ///   The unique tags assigned to the group by the user.
    /// </summary>
    public IReadOnlyList<string> UserTags { get; } = args.UserData.UserTags;

    #endregion

    #region Watch Stats

    /// <summary>
    ///   The number of episodes that have not been watched to completion.
    /// </summary>
    public int UnwatchedEpisodeCount { get; } = args.UserData.UnwatchedEpisodeCount;

    /// <summary>
    ///   The number of episodes that have been watched to completion.
    /// </summary>
    public int WatchedEpisodeCount { get; } = args.UserData.WatchedEpisodeCount;

    /// <summary>
    ///   The date and time when an episode in the group was last played to
    ///   completion.
    /// </summary>
    public DateTime? LastPlayedAt { get; } = args.UserData.LastPlayedAt?.ToUniversalTime();

    /// <summary>
    ///   The number of times any videos for episodes within the group has been
    ///   played to completion.
    /// </summary>
    public int PlaybackCount { get; } = args.UserData.PlaybackCount;

    #endregion

    #region Ordering / Filtering

    /// <summary>
    ///   Gets the date and time when user data for an episode linked to the
    ///   group was last updated, regardless of if it was watched to completion
    ///   or not. Can be used to determine continue watching and next-up order.
    /// </summary>
    public DateTime? LastEpisodeUpdatedAt { get; } = args.UserData.LastEpisodeUpdatedAt?.ToUniversalTime();

    /// <summary>
    ///   Gets the date and time when user data for a video linked to the group
    ///   was last updated, regardless of if it was watched to completion or not.
    /// </summary>
    public DateTime? LastVideoUpdatedAt { get; } = args.UserData.LastVideoUpdatedAt?.ToUniversalTime();

    #endregion

    /// <summary>
    ///   Indicates that the group has been watched to completion at least once
    ///   by the user.
    /// </summary>
    public bool IsWatched { get; } = args.UserData.UnwatchedEpisodeCount is 0;

    /// <summary>
    /// Gets the date and time when the user data was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; } = args.UserData.LastUpdatedAt.ToUniversalTime();
}
