using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// The reason the user data is being saved.
/// </summary>
public enum UserDataSaveReason
{
    /// <summary>
    /// The user data is being saved for no specific reason.
    /// </summary>
    None = 0,

    /// <summary>
    /// The user data is being saved because of user interaction.
    /// </summary>
    UserInteraction,

    /// <summary>
    /// The user data is being saved when playback of a video started.
    /// </summary>
    PlaybackStart,

    /// <summary>
    /// The user data is being saved when playback of a video was paused.
    /// </summary>
    PlaybackPause,

    /// <summary>
    /// The user data is being saved when playback of a video was resumed.
    /// </summary>
    PlaybackResume,

    /// <summary>
    /// The user data is being saved when playback of a video progressed.
    /// </summary>
    PlaybackProgress,

    /// <summary>
    /// The user data is being saved when playback of a video ended.
    /// </summary>
    PlaybackEnd,

    /// <summary>
    /// The user data is being imported from another source.
    /// </summary>
    /// <remarks>
    /// Should not be used directly in
    /// <see cref="IUserDataService.SaveVideoUserData(IShokoUser, IVideo, VideoUserDataUpdate, UserDataSaveReason, bool)"/>,
    /// as it should only be used in
    /// <see cref="IUserDataService.ImportVideoUserData(IShokoUser, IVideo, VideoUserDataUpdate, string, bool)"/>.
    /// Using it in the first method will result in the reason being turned into
    /// <see cref="None"/>.
    /// </remarks>
    Import,
}
