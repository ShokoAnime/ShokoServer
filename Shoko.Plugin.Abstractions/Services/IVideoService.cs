using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
///   Service responsible for interacting with videos and video files.
/// </summary>
public interface IVideoService
{
    #region Video File

    /// <summary>
    ///   Dispatched when a video file is first detected, either during a forced
    ///   import/scan or in a watched folder.
    ///   <br/>
    ///   Nothing has been done with the file yet.
    /// </summary>
    event EventHandler<FileDetectedEventArgs> VideoFileDetected;

    /// <summary>
    ///   Dispatched when a video file is deleted and removed from Shoko.
    /// </summary>
    event EventHandler<FileEventArgs> VideoFileDeleted;

    /// <summary>
    ///   Dispatched when a video file has been hashed. It is now ready to be
    ///   matched, and has been properly added to the database.
    /// </summary>
    event EventHandler<FileHashedEventArgs> VideoFileHashed;

    /// <summary>
    ///   Dispatched when a video file has been moved or renamed.
    /// </summary>
    event EventHandler<FileRelocatedEventArgs> VideoFileRelocated;

    /// <summary>
    ///   Gets all video files.
    /// </summary>
    /// <returns>
    ///   All video files.
    /// </returns>
    IEnumerable<IVideoFile> GetAllVideoFiles();

    /// <summary>
    ///   Looks up a video file by its ID.
    /// </summary>
    /// <param name="fileID">
    ///   The ID of the video file.
    /// </param>
    /// <returns>
    ///   The video file if found, otherwise <see langword="null"/>.
    /// </returns>
    IVideoFile? GetVideoFileByID(int fileID);

    /// <summary>
    ///   Looks up a video file by its absolute path.
    /// </summary>
    /// <param name="absolutePath">
    ///   The absolute path of the video file.
    /// </param>
    /// <returns>
    ///   The video file if found, otherwise <see langword="null"/>.
    /// </returns>
    IVideoFile? GetVideoFileByAbsolutePath(string absolutePath);

    /// <summary>
    ///   Looks up a video file by its relative path, optionally filtered by
    ///   managed folder in case the relative path is not unique enough by
    ///   itself.
    /// </summary>
    /// <param name="relativePath">
    ///   The relative path of the video file.
    /// </param>
    /// <param name="managedFolder">
    ///   The the managed folder.
    /// </param>
    /// <returns>
    ///   The video file if found, otherwise <see langword="null"/>.
    /// </returns>
    IVideoFile? GetVideoFileByRelativePath(string relativePath, IManagedFolder? managedFolder = null);

    /// <summary>
    ///   Notify the service that a new video file has been detected at the
    ///   absolute path. If the path is not inside of a managed folder it will
    ///   be ignored.
    /// </summary>
    /// <param name="absolutePath">
    ///   The absolute path.
    /// </param>
    /// <param name="addToMylist">
    ///   Optional. Set to <c>false</c> to not add the release to the user's
    ///   MyList if a release is found and saved.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="absolutePath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="absolutePath"/> is empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The path is outside of any managed folders.
    /// </exception>
    Task NotifyVideoFileChangeDetected(string absolutePath, bool addToMylist = true);

    /// <summary>
    ///   Notify the service that a new video file has been detected at the
    ///   relative path for the given managed folder.
    /// </summary>
    /// <param name="managedFolder">
    ///   The managed folder.
    /// </param>
    /// <param name="relativePath">
    ///   The relative path.
    /// </param>
    /// <param name="addToMylist">
    ///   Optional. Set to <c>false</c> to not add the release to the user's
    ///   MyList if a release is found and saved.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="managedFolder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="relativePath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="relativePath"/> is empty.
    /// </exception>
    Task NotifyVideoFileChangeDetected(IManagedFolder managedFolder, string relativePath, bool addToMylist = true);

    #endregion
    #region Video

    /// <summary>
    ///   Gets all videos.
    /// </summary>
    /// <returns>
    ///   A list of videos.
    /// </returns>
    IEnumerable<IVideo> GetAllVideos();

    /// <summary>
    ///   Looks up a video by its ID.
    /// </summary>
    /// <param name="videoID">
    ///   The ID of the video.
    /// </param>
    /// <returns>
    ///   The video if found, otherwise <see langword="null"/>.
    /// </returns>
    IVideo? GetVideoByID(int videoID);

    /// <summary>
    ///   Looks for a video by the <paramref name="hash"/> using the given
    ///   <paramref name="algorithm"/>. It will only return the video if it's
    ///   the only video with that hash.
    /// </summary>
    /// <param name="hash">The hash to look up the video by.</param>
    /// <param name="algorithm">The algorithm used to create the hash. Defaults to <c>"ED2K"</c>.</param>
    /// <returns>The video if found, otherwise <see langword="null"/>.</returns>
    IVideo? GetVideoByHash(string hash, string algorithm = "ED2K");

    /// <summary>
    ///   Looks up a video by the <paramref name="hash"/> and
    ///   <paramref name="fileSize"/>, where the hash is using the given
    ///   <paramref name="algorithm"/>. This is more foolproof than
    ///   <see cref="GetVideoByHash(string, string)"/> because we're also
    ///   factoring in the size of the file, giving us less overlap between
    ///   videos with the same hash for the same size.
    /// </summary>
    /// <param name="hash">
    ///   The hash to look up the video by.
    /// </param>
    /// <param name="fileSize">
    ///   The size of the video file.
    /// </param>
    /// <param name="algorithm">
    ///   The algorithm used to create the hash. Defaults to <c>"ED2K"</c>.
    /// </param>
    /// <returns>
    ///   The video if found, otherwise <see langword="null"/>.
    /// </returns>
    IVideo? GetVideoByHashAndSize(string hash, long fileSize, string algorithm = "ED2K");

    /// <summary>
    ///   Looks for videos by the <paramref name="hash"/> using the given
    ///   <paramref name="algorithm"/>. This will return all videos with that
    ///   hash.
    /// </summary>
    /// <param name="hash">
    ///   The hash to look up the video by.
    /// </param>
    /// <param name="algorithm">
    ///   The algorithm used to create the hash. Defaults to <c>"ED2K"</c>.
    /// </param>
    /// <returns>
    ///   The found videos.
    /// </returns>
    IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm = "ED2K");

    /// <summary>
    ///   Looks for videos by the <paramref name="hash"/> using the given
    ///   <paramref name="algorithm"/>. This will return all videos with that
    ///   hash.
    /// </summary>
    /// <param name="hash">
    ///   The hash to look up the video by.
    /// </param>
    /// <param name="algorithm">
    ///   The algorithm used to create the hash.
    /// </param>
    /// <param name="metadata">
    ///   The extra metadata to look for together with the hash. This will be a
    ///   case-insensitive match on the whole metadata string. Defaults to
    ///   <c>null</c>.
    /// </param>
    /// <returns>
    ///   The found videos.
    /// </returns>
    IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm, string? metadata);

    #endregion

    #region Managed Folders

    /// <summary>
    ///   Dispatched when a new managed folder is added.
    /// </summary>
    event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderAdded;

    /// <summary>
    ///   Dispatched when a managed folder is updated.
    /// </summary>
    event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderUpdated;

    /// <summary>
    ///   Dispatched when a managed folder is removed.
    /// </summary>
    event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderRemoved;

    /// <summary>
    ///   Gets all managed folders.
    /// </summary>
    /// <returns>A list of managed folders.</returns>
    IEnumerable<IManagedFolder> GetAllManagedFolders();

    /// <summary>
    ///   Looks up a managed folder by its ID.
    /// </summary>
    /// <param name="folderID">
    ///   The ID of the managed folder.
    /// </param>
    /// <returns>
    ///   The managed folder if found, otherwise <see langword="null"/>.
    /// </returns>
    IManagedFolder? GetManagedFolderByID(int folderID);

    /// <summary>
    ///   Looks up a managed folder by its absolute path.
    /// </summary>
    /// <param name="path">
    ///   The absolute path of the managed folder.
    /// </param>
    /// <returns>
    ///   The managed folder if found, otherwise <see langword="null"/>.
    /// </returns>
    IManagedFolder? GetManagedFolderByPath(string path);

    /// <summary>
    ///   Adds a new managed folder.
    /// </summary>
    /// <param name="path">
    ///   The path of the managed folder.
    /// </param>
    /// <param name="dropFolderType">
    ///   The drop folder type.
    /// </param>
    /// <param name="watchForNewFiles">
    ///   Whether to watch for new files in the managed folder.
    /// </param>
    /// <returns>
    ///   The added managed folder.
    /// </returns>
    IManagedFolder AddManagedFolder(string path, DropFolderType dropFolderType = DropFolderType.Excluded, bool watchForNewFiles = false);

    /// <summary>
    ///   Edits a managed folder.
    /// </summary>
    /// <param name="folder">
    ///   The managed folder to update.
    /// </param>
    /// <param name="path">
    ///   The new path of the managed folder.
    /// </param>
    /// <param name="dropFolderType">
    ///   The new drop folder type.
    /// </param>
    /// <param name="watchForNewFiles">
    ///   Whether to watch for new files in the managed folder.
    /// </param>
    /// <returns>
    ///   The updated managed folder.
    /// </returns>
    IManagedFolder UpdateManagedFolder(IManagedFolder folder, string? path = null, DropFolderType? dropFolderType = null, bool? watchForNewFiles = null);

    /// <summary>
    ///   Removes a managed folder.
    /// </summary>
    /// <param name="folder">
    ///   The managed folder to remove.
    /// </param>
    /// <param name="keepRecords">
    ///   Whether to keep the video records in the database. If this is set,
    ///   then video and video file records will be left intact and only the
    ///   managed folder record will be removed. This is for migration of files
    ///   to new locations.
    /// </param>
    /// <param name="removeMyList">
    ///   Whether to remove the video files in managed folder from the user's
    ///   AniDB MyList.
    /// </param>
    Task RemoveManagedFolder(IManagedFolder folder, bool keepRecords = false, bool removeMyList = true);

    /// <summary>
    ///   Scans a managed folder, scheduling jobs for new or all files within it
    ///   as needed.
    /// </summary>
    /// <param name="folder">
    ///   The managed folder to scan.
    /// </param>
    /// <param name="onlyNewFiles">
    ///   Whether to only scan for new files.
    /// </param>
    /// <param name="skipMylist">
    ///   Whether to skip adding the discovered files to the user's AniDB
    ///   MyList.
    /// </param>
    Task ScanManagedFolder(IManagedFolder folder, bool onlyNewFiles = false, bool skipMylist = false);

    /// <summary>
    ///   Schedules a scan of a managed folder, scheduling pre-processing jobs
    ///   for new or all files within it.
    /// </summary>
    /// <param name="folder">
    ///   The managed folder to scan.
    /// </param>
    /// <param name="onlyNewFiles">
    ///   Whether to only scan for new files.
    /// </param>
    /// <param name="skipMylist">
    ///   Whether to skip mylist adding the discovered files to the user's AniDB
    ///   MyList.
    /// </param>
    /// <param name="prioritize">
    ///   Whether to prioritize this job in the queue.
    /// </param>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task ScheduleScanForManagedFolder(IManagedFolder folder, bool onlyNewFiles = false, bool skipMylist = false, bool prioritize = true);

    /// <summary>
    ///   Scans all managed folders, scheduling pre-processing jobs for new or
    ///   all files within them.
    /// </summary>
    /// <param name="onlyDropSources">
    ///   Whether to only scan managed folder with
    ///   <see cref="DropFolderType.Source"/> set.
    /// </param>
    /// <param name="onlyNewFiles">
    ///   Whether to only scan for new video files.
    /// </param>
    /// <param name="skipMylist">
    ///   Whether to skip mylist adding the discovered files to the user's AniDB
    ///   MyList.
    /// </param>
    /// <param name="prioritize">
    ///   Whether to prioritize this job in the queue.
    /// </param>
    Task ScheduleScanForManagedFolders(bool onlyDropSources = false, bool? onlyNewFiles = null, bool skipMylist = false, bool prioritize = true);

    #endregion
}
