using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
/// Dispatched when movie data was updated.
/// </summary>
public class MovieInfoUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The reason this updated event was dispatched.
    /// </summary>
    public UpdateReason Reason { get; private set; }

    /// <summary>
    /// This is the full data. A diff was not performed for this.
    /// </summary>
    public IMovie MovieInfo { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MovieInfoUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="movieInfo">The movie info.</param>
    /// <param name="reason">The reason it was updated.</param>
    public MovieInfoUpdatedEventArgs(IMovie movieInfo, UpdateReason reason)
    {
        Reason = reason;
        MovieInfo = movieInfo;
    }

    /// <summary>
    ///   The API token of whoever caused the update, or <c>null</c>
    ///   when the system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
