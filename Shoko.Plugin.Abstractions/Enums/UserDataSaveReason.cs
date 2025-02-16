
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
    /// The user data is being saved because a video was imported, and the watch
    /// state from an existing video was copied over to the newly imported
    /// video.
    /// </summary>
    VideoReImport,

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
    /// The user data is being saved during an import from AniDB.
    /// </summary>
    AnidbImport,

    /// <summary>
    /// The user data is being saved because a video was imported from a generic
    /// source that's neither AniDB nor an existing video. For plugins to use
    /// when importing user data from other services.
    /// </summary>
    Import,
}
