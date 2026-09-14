using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Shoko.Server.Services;

/// <summary>
/// Long-path-safe file system access.
/// </summary>
/// <remarks>
/// Every file system call the server makes goes through this so that Windows' path length limit is
/// handled in one place. It is an interface so that callers which move or delete a user's files —
/// principally <see cref="VideoRelocationService"/> — can be exercised without touching a real disk.
/// </remarks>
public interface IFileSystemHelpers
{
    /// <summary>
    /// Checks whether a file exists, treating a null or empty path as absent.
    /// </summary>
    bool FileExists(string? path);

    /// <summary>
    /// Checks whether a directory exists, treating a null or empty path as absent.
    /// </summary>
    bool DirectoryExists(string? path);

    /// <summary>
    /// Deletes the file at the given path.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Deletes the directory at the given path.
    /// </summary>
    void DeleteDirectory(string path, bool recursive = false);

    /// <summary>
    /// Creates the directory at the given path, including any missing parents.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Moves a file from one path to another.
    /// </summary>
    void MoveFile(string sourcePath, string destinationPath);

    /// <summary>
    /// Opens the file at the given path for reading.
    /// </summary>
    FileStream OpenRead(string path);

    /// <summary>
    /// Gets the file size, or -1 if the file does not exist.
    /// </summary>
    long GetFileSize(string path);

    /// <summary>
    /// Gets a <see cref="FileInfo"/> for the path, or null if the file does not exist.
    /// </summary>
    FileInfo? GetFileInfo(string? path);

    /// <summary>
    /// Lists directory paths beneath the given directory.
    /// </summary>
    string[] GetDirectoryPaths(string directoryPath, bool recursive = false, Func<string, bool, bool>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists file paths beneath the given directory.
    /// </summary>
    string[] GetFilePaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, Func<string, bool, bool>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a predicate matching paths against the given extensions and filter.
    /// </summary>
    Func<string, bool, bool> GetPathValidator(IEnumerable<string>? extensions, Func<string, bool, bool>? filter);

    /// <summary>
    /// Gets the inode number (Unix) or file ID (Windows) for a file, or null if it cannot be obtained.
    /// </summary>
    long? GetVideoFileUID(string? path);
}
