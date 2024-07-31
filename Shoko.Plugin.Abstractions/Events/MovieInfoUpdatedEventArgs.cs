using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Fired when movie data was updated.
/// </summary>
public class MovieInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The reason this updated event was dispatched.
    /// </summary>
    public UpdateReason Reason { get; }

    /// <summary>
    /// This is the full data. A diff was not performed for this.
    /// </summary>
    public IMovie MovieInfo { get; }

    public MovieInfoUpdatedEventArgs(IMovie movieInfo, UpdateReason reason)
    {
        Reason = reason;
        MovieInfo = movieInfo;
    }
}
