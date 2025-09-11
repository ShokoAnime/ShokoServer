using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class VideoUserDataSavedSignalRModel(VideoUserDataSavedEventArgs args)
{
    /// <summary>
    /// The ID of the user which had their data updated.
    /// </summary>
    public int UserID { get; } = args.User.ID;

    /// <summary>
    /// The ID of the video which had its user data updated.
    /// </summary>
    public int VideoID { get; } = args.Video.ID;

    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UserDataSaveReason Reason { get; } = args.Reason;

    /// <summary>
    /// Indicates that the user data was imported from a source.
    /// </summary>
    public bool IsImport { get; } = args.IsImport;

    /// <summary>
    /// The source if the <see cref="Reason"/> is
    /// <see cref="UserDataSaveReason.Import"/>.
    /// </summary>
    public string? ImportSource { get; } = args.ImportSource;

    /// <summary>
    /// Gets the number of times the video has been played.
    /// </summary>
    public int PlaybackCount { get; } = args.UserData.PlaybackCount;

    /// <summary>
    /// Gets the position in the video where playback was last resumed.
    /// </summary>
    public TimeSpan ResumePosition { get; } = args.UserData.ResumePosition;

    /// <summary>
    /// Gets the date and time when the video was last played.
    /// </summary>
    public DateTime? LastPlayedAt { get; } = args.UserData.LastPlayedAt?.ToUniversalTime();
}
