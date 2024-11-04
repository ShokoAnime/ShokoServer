using System;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// Service responsible for interacting with videos and video files.
/// </summary>
public interface IVideoService
{
    #region Video File

    /// <summary>
    /// Dispatched when a video file is first detected, either during a forced
    /// import/scan or in a watched folder.
    /// <br/>
    /// Nothing has been done with the file yet.
    /// </summary>
    event EventHandler<FileDetectedEventArgs> VideoFileDetected;

    /// <summary>
    /// Dispatched when a video file is deleted and removed from Shoko.
    /// </summary>
    event EventHandler<FileEventArgs> VideoFileDeleted;

    /// <summary>
    /// Dispatched when a video file has been hashed. It is now ready to be
    /// matched, and has been properly added to the database.
    /// </summary>
    event EventHandler<FileEventArgs> VideoFileHashed;

    /// <summary>
    /// Dispatched when cross references are made and data is gathered for a
    /// video file. This has most if not all relevant data for a file, but
    /// is not necessarily complete, e.g. if we failed to download the metadata
    /// for the linked series or episodes. In that case, then use
    /// <see cref="IMetadataService.EpisodeUpdated"/> with a filter on the data
    /// source and episode id to ensure the desired data is gathered before
    /// doing your thing.
    /// </summary>
    event EventHandler<FileEventArgs> VideoFileMatched;

    /// <summary>
    /// Dispatched when a video file has been scanned but no changes to the
    /// cross-references were made. It can be because the file is unrecognized,
    /// or because there was no changes to the existing cross-references linked
    /// to the video file.
    /// </summary>
    event EventHandler<FileNotMatchedEventArgs> VideoFileNotMatched;

    /// <summary>
    /// Dispatched when a video file has been renamed.
    /// </summary>
    event EventHandler<FileRenamedEventArgs> VideoFileRenamed;

    /// <summary>
    /// Dispatched when a video file has been moved.
    /// </summary>
    event EventHandler<FileMovedEventArgs> VideoFileMoved;

    /// <summary>
    /// Gets all video files as a queryable list.
    /// </summary>
    /// <returns>All video files.</returns>
    IQueryable<IVideoFile> GetAllVideoFiles();

    /// <summary>
    /// Looks up a video file by its ID.
    /// </summary>
    /// <param name="fileID">The ID of the video file.</param>
    /// <returns>The video file if found, otherwise <see langword="null"/>.</returns>
    IVideoFile? GetVideoFileByID(int fileID);

    /// <summary>
    /// Looks up a video file by its absolute path.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the video file.</param>
    /// <returns>The video file if found, otherwise <see langword="null"/>.</returns>
    IVideoFile? GetVideoFileByAbsolutePath(string absolutePath);

    /// <summary>
    /// Looks up a video file by its relative path, optionally filtered by
    /// import folder in case the relative path is not unique enough by itself.
    /// </summary>
    /// <param name="relativePath">The relative path of the video file.</param>
    /// <param name="importFolderID">The ID of the import folder.</param>
    /// <returns>The video file if found, otherwise <see langword="null"/>.</returns>
    IVideoFile? GetVideoFileByRelativePath(string relativePath, int? importFolderID = null);

    #endregion
    #region Video

    /// <summary>
    /// Gets all videos as a queryable list.
    /// </summary>
    /// <returns>A queryable list of videos.</returns>
    IQueryable<IVideo> GetAllVideos();

    /// <summary>
    /// Looks up a video by its ID.
    /// 
    /// </summary>
    /// <param name="videoID">The ID of the video.</param>
    /// <returns>The video if found, otherwise <see langword="null"/>.</returns>
    IVideo? GetVideoByID(int videoID);

    /// <summary>
    /// Looks up a video by the <paramref name="hash"/> using the given
    /// <paramref name="algorithm"/>.
    /// </summary>
    /// <param name="hash">The hash to look up the video by.</param>
    /// <param name="algorithm">The algorithm used to create the hash. Defaults to <see cref="HashAlgorithmName.ED2K"/>.</param>
    /// <returns>The video if found, otherwise <see langword="null"/>.</returns>
    IVideo? GetVideoByHash(string hash, HashAlgorithmName algorithm = HashAlgorithmName.ED2K);

    /// <summary>
    /// Looks up a video by the <paramref name="hash"/> and
    /// <paramref name="fileSize"/>, where the hash is using the given
    /// <paramref name="algorithm"/>. This is more foolproof than
    /// <see cref="GetVideoByHash(string, HashAlgorithmName)"/> because we're also
    /// factoring in the size of the file, giving us less overlap between videos
    /// with the same hash for the same size.
    /// </summary>
    /// <param name="hash">The hash to look up the video by.</param>
    /// <param name="fileSize">The size of the video file.</param>
    /// <param name="algorithm">The algorithm used to create the hash. Defaults to <see cref="HashAlgorithmName.ED2K"/>.</param>
    /// <returns>The video if found, otherwise <see langword="null"/>.</returns>
    IVideo? GetVideoByHashAndSize(string hash, long fileSize, HashAlgorithmName algorithm = HashAlgorithmName.ED2K);

    #endregion
}
