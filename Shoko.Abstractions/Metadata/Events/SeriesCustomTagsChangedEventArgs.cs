using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when custom tags are added to or removed from a series.
/// </summary>
public class SeriesCustomTagsChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The series.
    /// </summary>
    public required IShokoSeries Series { get; init; }

    /// <summary>
    ///   The custom tags added to the series.
    /// </summary>
    public IReadOnlyList<IShokoTag> Added { get; init; } = [];

    /// <summary>
    ///   The custom tags removed from the series.
    /// </summary>
    public IReadOnlyList<IShokoTag> Removed { get; init; } = [];

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
