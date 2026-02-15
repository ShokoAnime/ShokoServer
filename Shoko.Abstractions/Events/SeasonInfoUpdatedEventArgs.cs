using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when season data was updated.
/// </summary>
/// <remarks>
/// Currently, these will fire a lot in succession, as these are updated in batch with a series.
/// </remarks>
public class SeasonInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The reason this updated event was dispatched.
    /// </summary>
    public UpdateReason Reason { get; }

    /// <summary>
    /// This is the full data. A diff was not performed for this.
    /// </summary>
    public ISeason SeasonInfo { get; }

    /// <summary>
    /// This is the full data. A diff was not performed for this.
    ///
    /// This is provided for convenience.
    /// </summary>
    public ISeries SeriesInfo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonInfoUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="seriesInfo">The series info.</param>
    /// <param name="seasonInfo">The season info.</param>
    /// <param name="reason">The reason it was updated.</param>
    public SeasonInfoUpdatedEventArgs(ISeries seriesInfo, ISeason seasonInfo, UpdateReason reason)
    {
        Reason = reason;
        SeriesInfo = seriesInfo;
        SeasonInfo = seasonInfo;
    }
}
