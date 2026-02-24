using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.UserData.Enums;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when episode user data was updated.
/// </summary>
public class EpisodeUserDataSavedEventArgs : EventArgs
{
    /// <summary>
    /// The video reason why the episode user data was updated, if this update
    /// was caused by a video update.
    /// </summary>
    public required VideoUserDataSaveReason VideoReason { get; init; }

    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    public required EpisodeUserDataSaveReason Reason { get; init; }

    /// <summary>
    /// Indicates that the user data was imported from another source.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ImportSource))]
    public bool IsImport => Reason.HasFlag(EpisodeUserDataSaveReason.Import) && !string.IsNullOrEmpty(ImportSource);

    /// <summary>
    /// The source if the <see cref="Reason"/> has the
    /// <see cref="EpisodeUserDataSaveReason.Import">Import flag</see> set.
    /// </summary>
    public string? ImportSource { get; init; }

    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public required IUser User { get; init; }

    /// <summary>
    /// The episode which had its user data updated.
    /// </summary>
    public required IShokoEpisode Episode { get; init; }

    /// <summary>
    /// The updated episode user data.
    /// </summary>
    public required IEpisodeUserData UserData { get; init; }
}
