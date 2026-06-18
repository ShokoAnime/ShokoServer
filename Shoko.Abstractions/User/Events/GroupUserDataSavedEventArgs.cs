using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User.Enums;

namespace Shoko.Abstractions.User.Events;

/// <summary>
/// Dispatched when group user data was updated.
/// </summary>
public class GroupUserDataSavedEventArgs : EventArgs
{
    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    public required GroupUserDataSaveReason Reason { get; init; }

    /// <summary>
    /// Indicates that the user data was imported from another source.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ImportSource))]
    public bool IsImport => Reason.HasFlag(GroupUserDataSaveReason.Import) && !string.IsNullOrEmpty(ImportSource);

    /// <summary>
    /// The source if the <see cref="Reason"/> has the
    /// <see cref="GroupUserDataSaveReason.Import">Import flag</see> set.
    /// </summary>
    public string? ImportSource { get; init; }

    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public required IUser User { get; init; }

    /// <summary>
    /// The group which had its user data updated.
    /// </summary>
    public required IShokoGroup Group { get; init; }

    /// <summary>
    /// The updated group user data.
    /// </summary>
    public required IGroupUserData UserData { get; init; }
}
