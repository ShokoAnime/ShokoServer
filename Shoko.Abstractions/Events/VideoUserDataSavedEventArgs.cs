using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when video user data was updated.
/// </summary>
public class VideoUserDataSavedEventArgs : EventArgs
{
    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    public VideoUserDataSaveReason Reason { get; }

    /// <summary>
    /// Indicates that the user data was imported from another source.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ImportSource))]
    public bool IsImport => Reason is VideoUserDataSaveReason.Import && !string.IsNullOrEmpty(ImportSource);

    /// <summary>
    /// The source if the <see cref="Reason"/> is
    /// <see cref="VideoUserDataSaveReason.Import"/>.
    /// </summary>
    public string? ImportSource { get; }

    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public IUser User { get; }

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
    /// <param name="importSource">The source if the <paramref name="reason"/> is <see cref="VideoUserDataSaveReason.Import"/>.</param>
    public VideoUserDataSavedEventArgs(VideoUserDataSaveReason reason, IUser user, IVideo video, IVideoUserData userData, string? importSource = null)
    {
        if (reason is VideoUserDataSaveReason.Import && string.IsNullOrWhiteSpace(importSource))
            reason = VideoUserDataSaveReason.None;
        else if (!string.IsNullOrWhiteSpace(importSource))
            importSource = null;

        Reason = reason;
        ImportSource = importSource;
        User = user;
        Video = video;
        UserData = userData;
    }
}
