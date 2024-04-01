using System;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Fired on series info updates, currently, AniDB, TvDB, etc will trigger this
/// </summary>
public class SeriesInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Anime info. This is the full data. A diff was not performed for this
    /// </summary>
    public ISeries SeriesInfo { get; }

    public SeriesInfoUpdatedEventArgs(ISeries seriesInfo)
    {
        SeriesInfo = seriesInfo;
    }
}
