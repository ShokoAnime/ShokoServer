using System;

namespace Shoko.Plugin.Abstractions
{
    public interface IShokoEventHandler
    {
        /// <summary>
        /// Fired when a file is deleted and removed from Shoko.
        /// </summary>
        public event EventHandler<FileDeletedEventArgs> FileDeleted;
        /// <summary>
        /// Fired when a file is detected, either during a forced import/scan or a watched folder.
        /// Nothing has been done with the file yet here.
        /// </summary>
        event EventHandler<FileDetectedEventArgs> FileDetected;
        /// <summary>
        /// Fired when a file is hashed. Has hashes and stuff.
        /// </summary>
        event EventHandler<FileHashedEventArgs> FileHashed;
        /// <summary>
        /// Fired when a cross reference is made and data is gathered for a file. This has most if not all relevant data for a file. TvDB may take longer.
        /// Use <see cref="EpisodeUpdated"/> with a filter on the data source to ensure the desired data is gathered.
        /// </summary>
        event EventHandler<FileMatchedEventArgs> FileMatched;
        /// <summary>
        /// Fired when an AniDB Ban happens...and it will.
        /// </summary>
        event EventHandler<AniDBBannedEventArgs> AniDBBanned;
        /// <summary>
        /// Fired on series info updates. Currently, AniDB, TvDB, etc will trigger this.
        /// </summary>
        event EventHandler<SeriesInfoUpdatedEventArgs> SeriesUpdated;
        /// <summary>
        /// Fired on episode info updates. Currently, AniDB, TvDB, etc will trigger this.
        /// </summary>
        event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeUpdated;
        /// <summary>
        /// Fired when the core settings has been saved.
        /// </summary>
        event EventHandler<SettingsSavedEventArgs> SettingsSaved;
    }
}
