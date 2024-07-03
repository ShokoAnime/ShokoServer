using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Currently, these will fire a lot in succession, as these are updated in batch with a series.
/// </summary>
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
    /// This is provided for convenience, use <see cref="IShokoEventHandler.SeriesUpdate"/>
    /// </summary>
    public ISeries SeriesInfo { get; }

    public EpisodeInfoUpdatedEventArgs(ISeries seriesInfo, IEpisode episodeInfo, UpdateReason reason)
    {
        Reason = reason;
        SeriesInfo = seriesInfo;
        EpisodeInfo = episodeInfo;
    }
}
