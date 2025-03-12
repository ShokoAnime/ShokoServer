using System;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Interface for Shoko event handlers.
/// </summary>
public interface IShokoEventHandler
{
    /// <summary>
    /// Fired when a file is deleted and removed from Shoko.
    /// </summary>
    event EventHandler<FileEventArgs> FileDeleted;

    /// <summary>
    /// Fired when a file is detected, either during a forced import/scan or a watched folder.
    /// Nothing has been done with the file yet here.
    /// </summary>
    event EventHandler<FileDetectedEventArgs> FileDetected;

    /// <summary>
    /// Fired when a file is renamed
    /// </summary>
    event EventHandler<FileRenamedEventArgs> FileRenamed;

    /// <summary>
    /// Fired when a file is moved
    /// </summary>
    event EventHandler<FileMovedEventArgs> FileMoved;

    /// <summary>
    /// Fired on series info updates. Currently, AniDB, TMDB, etc will trigger this.
    /// </summary>
    event EventHandler<SeriesInfoUpdatedEventArgs> SeriesUpdated;

    /// <summary>
    /// Fired on episode info updates. Currently, AniDB, TMDB, etc will trigger this.
    /// </summary>
    event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeUpdated;

    /// <summary>
    /// Fired on movie info updates. Currently only TMDB will trigger this.
    /// </summary>
    event EventHandler<MovieInfoUpdatedEventArgs> MovieUpdated;

    /// <summary>
    /// Fired when the the server has fully started and all services are usable.
    /// </summary>
    event EventHandler Started;

    /// <summary>
    /// Fired when the the server is shutting down.
    /// </summary>
    event EventHandler Shutdown;
}
