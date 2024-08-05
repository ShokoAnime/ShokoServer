using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Fired on series info updates, currently, AniDB, TvDB, etc will trigger this
/// </summary>
public class SeriesInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The reason this updated event was dispatched.
    /// </summary>
    public UpdateReason Reason { get; }

    /// <summary>
    /// Anime info. This is the full data. A diff was not performed for this
    /// </summary>
    public ISeries SeriesInfo { get; }

    public SeriesInfoUpdatedEventArgs(ISeries seriesInfo, UpdateReason reason)
    {
        Reason = reason;
        SeriesInfo = seriesInfo;
    }
}
