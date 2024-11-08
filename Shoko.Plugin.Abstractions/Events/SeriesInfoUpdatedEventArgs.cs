using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a series metadata update occurs.
/// </summary>
public class SeriesInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The reason this updated event was dispatched.
    /// </summary>
    public UpdateReason Reason { get; private set; }

    /// <summary>
    /// Anime info. This is the full data. A diff was not performed for this
    /// </summary>
    public ISeries SeriesInfo { get; private set; }

    /// <summary>
    /// The episodes that were added/updated/removed during this event.
    /// </summary>
    public IReadOnlyList<EpisodeInfoUpdatedEventArgs> Episodes { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesInfoUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="seriesInfo">The series info.</param>
    /// <param name="reason">The reason it was updated.</param>
    /// <param name="episodes">The episodes that were added/updated/removed.</param>
    public SeriesInfoUpdatedEventArgs(ISeries seriesInfo, UpdateReason reason, IEnumerable<EpisodeInfoUpdatedEventArgs>? episodes = null)
    {
        Reason = reason;
        SeriesInfo = seriesInfo;
        Episodes = episodes?.ToList() ?? [];
    }
}
