using System;
using System.Collections.Generic;
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
    /// Dispatched when a video file has been moved or renamed.
    /// </summary>
    event EventHandler<FileMovedEventArgs> VideoFileRelocated;

    /// <summary>
    /// Gets all video files.
    /// </summary>
    /// <returns>All video files.</returns>
    IEnumerable<IVideoFile> GetAllVideoFiles();

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
    /// managed folder in case the relative path is not unique enough by itself.
    /// </summary>
    /// <param name="relativePath">The relative path of the video file.</param>
    /// <param name="managedFolderID">The ID of the managed folder.</param>
    /// <returns>The video file if found, otherwise <see langword="null"/>.</returns>
    IVideoFile? GetVideoFileByRelativePath(string relativePath, int? managedFolderID = null);

    #endregion
    #region Video

    /// <summary>
    /// Gets all videos.
    /// </summary>
    /// <returns>A list of videos.</returns>
    IEnumerable<IVideo> GetAllVideos();

    /// <summary>
    /// Looks up a video by its ID.
    /// 
    /// </summary>
    /// <param name="videoID">The ID of the video.</param>
    /// <returns>The video if found, otherwise <see langword="null"/>.</returns>
    IVideo? GetVideoByID(int videoID);

    /// <summary>
    /// Looks for a video by the <paramref name="hash"/> using the given
    /// <paramref name="algorithm"/>. It will only return the video if it's
    /// the only video with that hash.
    /// </summary>
    /// <param name="hash">The hash to look up the video by.</param>
    /// <param name="algorithm">The algorithm used to create the hash. Defaults to <c>"ED2K"</c>.</param>
    /// <returns>The video if found, otherwise <see langword="null"/>.</returns>
    IVideo? GetVideoByHash(string hash, string algorithm = "ED2K");

    /// <summary>
    /// Looks up a video by the <paramref name="hash"/> and
    /// <paramref name="fileSize"/>, where the hash is using the given
    /// <paramref name="algorithm"/>. This is more foolproof than
    /// <see cref="GetVideoByHash(string, string)"/> because we're also
    /// factoring in the size of the file, giving us less overlap between videos
    /// with the same hash for the same size.
    /// </summary>
    /// <param name="hash">The hash to look up the video by.</param>
    /// <param name="fileSize">The size of the video file.</param>
    /// <param name="algorithm">The algorithm used to create the hash. Defaults to <c>"ED2K"</c>.</param>
    /// <returns>The video if found, otherwise <see langword="null"/>.</returns>
    IVideo? GetVideoByHashAndSize(string hash, long fileSize, string algorithm = "ED2K");

    /// <summary>
    /// Looks for videos by the <paramref name="hash"/> using the given
    /// <paramref name="algorithm"/>. This will return all videos with that
    /// hash.
    /// </summary>
    /// <param name="hash">The hash to look up the video by.</param>
    /// <param name="algorithm">The algorithm used to create the hash. Defaults to <c>"ED2K"</c>.</param>
    /// <returns>The found videos.</returns>
    IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm = "ED2K");

    /// <summary>
    /// Looks for videos by the <paramref name="hash"/> using the given
    /// <paramref name="algorithm"/>. This will return all videos with that
    /// hash.
    /// </summary>
    /// <param name="hash">The hash to look up the video by.</param>
    /// <param name="metadata">The extra metadata to look for together with the hash. This will be a case-insensitive match on the whole metadata string. Defaults to <c>null</c>.</param>
    /// <param name="algorithm">The algorithm used to create the hash. </param>
    /// <returns>The found videos.</returns>
    IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm, string? metadata);

    #endregion

    #region Managed Folders

    /// <summary>
    /// Dispatched when a new managed folder is added.
    /// </summary>
    event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderAdded;

    /// <summary>
    /// Dispatched when a managed folder is updated.
    /// </summary>
    event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderUpdated;

    /// <summary>
    /// Dispatched when a managed folder is removed.
    /// </summary>
    event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderRemoved;

    /// <summary>
    /// Gets all managed folders.
    /// </summary>
    /// <returns>A list of managed folders.</returns>
    IEnumerable<IManagedFolder> GetAllManagedFolders();

    /// <summary>
    /// Looks up a managed folder by its ID.
    /// </summary>
    /// <param name="folderID">The ID of the managed folder.</param>
    /// <returns>The managed folder if found, otherwise <see langword="null"/>.</returns>
    IManagedFolder? GetManagedFolderByID(int folderID);

    #endregion
}
