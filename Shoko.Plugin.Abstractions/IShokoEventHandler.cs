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
    /// Fired when a file is hashed. Has hashes and stuff.
    /// </summary>
    event EventHandler<FileEventArgs> FileHashed;

    /// <summary>
    /// Fired when a file is scanned but no changes to the cross-reference
    /// were made. It can be because the file is unrecognized, or because
    /// there was no changes to the existing cross-references linked to the
    /// file.
    /// </summary>
    event EventHandler<FileNotMatchedEventArgs> FileNotMatched;

    /// <summary>
    /// Fired when a cross reference is made and data is gathered for a file. This has most if not all relevant data for a file.
    /// Use <see cref="EpisodeUpdated"/> with a filter on the data source to ensure the desired data is gathered.
    /// </summary>
    event EventHandler<FileEventArgs> FileMatched;

    /// <summary>
    /// Fired when a file is renamed
    /// </summary>
    event EventHandler<FileRenamedEventArgs> FileRenamed;

    /// <summary>
    /// Fired when a file is moved
    /// </summary>
    event EventHandler<FileMovedEventArgs> FileMoved;

    /// <summary>
    /// Fired when an AniDB Ban happens...and it will.
    /// </summary>
    event EventHandler<AniDBBannedEventArgs> AniDBBanned;

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
    /// Fired when the core settings has been saved.
    /// </summary>
    event EventHandler<SettingsSavedEventArgs> SettingsSaved;

    /// <summary>
    /// Fired when an avdump event occurs.
    /// </summary>
    event EventHandler<AVDumpEventArgs> AVDumpEvent;
}
