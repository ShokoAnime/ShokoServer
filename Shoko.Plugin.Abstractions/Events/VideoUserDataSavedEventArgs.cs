
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when video user data was updated.
/// </summary>
public class VideoUserDataSavedEventArgs
{
    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    public UserDataSaveReason Reason { get; }

    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public IShokoUser User { get; }

    /// <summary>
    /// The video which had its user data updated.
    /// </summary>
    public IVideo Video { get; }

    /// <summary>
    /// The updated video user data.
    /// </summary>
    public IVideoUserData UserData { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoUserDataSavedEventArgs"/> class.
    /// </summary>
    /// <param name="reason">The reason why the user data was updated.</param>
    /// <param name="user">The user which had their data updated.</param>
    /// <param name="video">The video which had its user data updated.</param>
    /// <param name="userData">The updated video user data.</param>
    public VideoUserDataSavedEventArgs(UserDataSaveReason reason, IShokoUser user, IVideo video, IVideoUserData userData)
    {
        Reason = reason;
        User = user;
        Video = video;
        UserData = userData;
    }
}
