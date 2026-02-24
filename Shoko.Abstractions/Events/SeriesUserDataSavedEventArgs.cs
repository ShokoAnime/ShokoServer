using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.UserData.Enums;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when series user data was updated.
/// </summary>
public class SeriesUserDataSavedEventArgs : EventArgs
{
    /// <summary>
    /// The video reason why the series user data was updated, if this update
    /// was caused by a video update.
    /// </summary>
    public required VideoUserDataSaveReason VideoReason { get; init; }

    /// <summary>
    /// The reason why the user data was updated.
    /// </summary>
    public required SeriesUserDataSaveReason Reason { get; init; }

    /// <summary>
    /// Indicates that the user data was imported from another source.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ImportSource))]
    public bool IsImport => Reason.HasFlag(SeriesUserDataSaveReason.Import) && !string.IsNullOrEmpty(ImportSource);

    /// <summary>
    /// The source if the <see cref="Reason"/> has the
    /// <see cref="SeriesUserDataSaveReason.Import">Import flag</see> set.
    /// </summary>
    public string? ImportSource { get; init; }

    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public required IUser User { get; init; }

    /// <summary>
    /// The series which had its user data updated.
    /// </summary>
    public required IShokoSeries Series { get; init; }

    /// <summary>
    /// The updated series user data.
    /// </summary>
    public required ISeriesUserData UserData { get; init; }
}
