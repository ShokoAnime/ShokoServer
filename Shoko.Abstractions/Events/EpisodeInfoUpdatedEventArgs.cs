using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when episode data was updated.
/// </summary>
/// <remarks>
/// Currently, these will fire a lot in succession, as these are updated in batch with a series.
/// </remarks>
public class EpisodeInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The reason this updated event was dispatched.
    /// </summary>
    public UpdateReason Reason { get; }

    /// <summary>
    /// This is the full data. A diff was not performed for this.
    /// </summary>
    public IEpisode EpisodeInfo { get; }

    /// <summary>
    /// This is the full data. A diff was not performed for this.
    ///
    /// This is provided for convenience.
    /// </summary>
    public ISeries SeriesInfo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeInfoUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="seriesInfo">The series info.</param>
    /// <param name="episodeInfo">The episode info.</param>
    /// <param name="reason">The reason it was updated.</param>
    public EpisodeInfoUpdatedEventArgs(ISeries seriesInfo, IEpisode episodeInfo, UpdateReason reason)
    {
        Reason = reason;
        SeriesInfo = seriesInfo;
        EpisodeInfo = episodeInfo;
    }
}
