using System;

namespace Shoko.Renamer.Abstractions.DataModels
{
    public interface IAniDBFile
    {
        /// <summary>
        /// The ID of the file on AniDB
        /// </summary>
        int AniDBFileID { get; }
        /// <summary>
        /// Info about the release group of the file
        /// </summary>
        IReleaseGroup ReleaseGroup { get; }
        /// <summary>
        /// Where the file was ripped from, bluray, dvd, etc
        /// </summary>
        string Source { get; }
        /// <summary>
        /// Description of the file on AniDB. This will often be blank, and it's generally not useful
        /// </summary>
        string Description { get; }
        /// <summary>
        /// When the file was released, according to AniDB. This will be wrong for a lot of older or less popular anime
        /// </summary>
        DateTime? ReleaseDate { get; }
        /// <summary>
        /// Usually 1. Sometimes 2. 3 happens. It's incremented when a release is updated due to errors 
        /// </summary>
        int Version { get; }
        /// <summary>
        /// This is mostly for hentai, and it's often wrong.
        /// </summary>
        bool Censored { get; }
    }
}