using System;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Currently, these will fire a lot in succession, as these are updated in batch with a series.
/// </summary>
public class EpisodeInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// This is the full data. A diff was not performed for this.
    /// This is provided for convenience, use <see cref="IShokoEventHandler.SeriesUpdate"/>
    /// </summary>
    public ISeries SeriesInfo { get; }

    /// <summary>
    /// This is the full data. A diff was not performed for this.
    /// </summary>
    public IEpisode EpisodeInfo { get; }

    public EpisodeInfoUpdatedEventArgs(ISeries seriesInfo, IEpisode episodeInfo)
    {
        SeriesInfo = seriesInfo;
        EpisodeInfo = episodeInfo;
    }
}
