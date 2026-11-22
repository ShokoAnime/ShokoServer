using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Shoko.Abstractions.FileSystem;

namespace Shoko.Abstractions.FileSystem.Services;

/// <summary>
///   File system access the way the server does it: safe for long paths on
///   Windows, and able to tell when two paths are the same file.
/// </summary>
/// <remarks>
///   <para>
///     Every call takes absolute paths. None of them creates, moves or
///     removes a video's location record.
///   </para>
///   <para>
///     The changes made here (copies, moves, deletions, new directories and
///     links) are hidden from the server's file watcher while they run, so a
///     file moved here is not taken for a new one. A video file made in a
///     managed folder this way is found by the next scan, or at once when
///     handed to <see cref="Video.Services.IVideoService.NotifyVideoFileChangeDetected(string, bool, bool)"/>.
///     A file written through <see cref="OpenFile"/> is not hidden.
///   </para>
/// </remarks>
public interface IFileSystem
{
    #region Files

    /// <summary>
    ///   Checks whether a file exists, treating a null or empty path as absent.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><c>true</c> when a file exists at the path.</returns>
    bool FileExists(string? path);

    /// <summary>
    ///   Gets a <see cref="FileInfo"/> for the path, or <c>null</c> if the file
    ///   does not exist.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The file's info, or <c>null</c>.</returns>
    FileInfo? GetFileInfo(string? path);

    /// <summary>
    ///   Reads what the file system says about the file at the given path:
    ///   <see cref="GetStat"/>, when it is a file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="followSymbolicLinks">
    ///   Whether a symbolic link answers for what it points to; when not, a
    ///   link gives <c>null</c>.
    /// </param>
    /// <returns>
    ///   The file's info, or <c>null</c> when nothing is at the path or it is
    ///   not a file.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">The entry may not be read.</exception>
    /// <exception cref="IOException">It could not be read for another reason.</exception>
    FileStat? GetFileStat(string? path, bool followSymbolicLinks = true)
        => GetStat(path, followSymbolicLinks) as FileStat;

    /// <summary>
    ///   Gets the file size, or -1 if the file does not exist.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The size in bytes, or -1.</returns>
    long GetFileSize(string path);

    /// <summary>
    ///   Opens the file at the given path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="mode">How to open or create it.</param>
    /// <param name="access">What the stream may do.</param>
    /// <param name="share">What others may do with the file meanwhile.</param>
    /// <returns>The stream, which the caller disposes.</returns>
    /// <exception cref="ArgumentException">The path is empty, or <paramref name="mode"/> and <paramref name="access"/> do not go together, such as <see cref="FileMode.Append"/> with read access.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be opened with that access, or the path is a directory.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="mode"/> needs the file and it does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder holding it does not exist.</exception>
    /// <exception cref="IOException">It could not be opened for another reason: another process holds it, it exists while <paramref name="mode"/> is <see cref="FileMode.CreateNew"/>, or the volume could not be reached.</exception>
    FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share);

    /// <summary>
    ///   Opens the file at the given path for reading.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The stream, which the caller disposes.</returns>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be read, or the path is a directory.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder holding it does not exist.</exception>
    /// <exception cref="IOException">It could not be opened for another reason: another process holds it, or the volume could not be reached.</exception>
    FileStream OpenRead(string path);

    /// <summary>
    ///   Copies a file to another path.
    /// </summary>
    /// <param name="sourcePath">The file to copy.</param>
    /// <param name="destinationPath">Where to copy it.</param>
    /// <param name="overwrite">
    ///   Whether to replace a file already at the destination. Otherwise one
    ///   there throws an <see cref="IOException"/>.
    /// </param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">The source may not be read or the destination written, or the destination is a directory.</exception>
    /// <exception cref="FileNotFoundException">The source does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">A folder on either path does not exist.</exception>
    /// <exception cref="IOException">A file is at the destination and <paramref name="overwrite"/> is <c>false</c>, or it could not be copied for another reason.</exception>
    void CopyFile(string sourcePath, string destinationPath, bool overwrite = false);

    /// <summary>
    ///   Moves a file from one path to another, across volumes too.
    /// </summary>
    /// <remarks>
    ///   A move that only changes the case of its name (<c>afile.mkv</c> to
    ///   <c>AFile.mkv</c>) renames it even on a case-insensitive file system,
    ///   where both name the same file, by going through a temporary name.
    ///   On one volume, replacing a file with <paramref name="overwrite"/> is
    ///   a single step: a reader sees the old file or the new one, never
    ///   neither. A link is moved as itself.
    /// </remarks>
    /// <param name="sourcePath">The file to move.</param>
    /// <param name="destinationPath">Where to move it.</param>
    /// <param name="overwrite">
    ///   Whether to replace a file already at the destination. Otherwise one
    ///   there throws an <see cref="IOException"/>.
    /// </param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be moved, or the destination is a directory.</exception>
    /// <exception cref="FileNotFoundException">The source does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">A folder on either path does not exist.</exception>
    /// <exception cref="IOException">
    ///   A file is at the destination and <paramref name="overwrite"/> is
    ///   <c>false</c>, or it could not be moved for another reason.
    /// </exception>
    void MoveFile(string sourcePath, string destinationPath, bool overwrite = false);

    /// <summary>
    ///   Deletes the file at the given path. Nothing happens when no file is
    ///   there.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be deleted (on Windows, it is read-only), or the path is a directory.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder holding it does not exist.</exception>
    /// <exception cref="IOException">It could not be deleted for another reason, such as another process holding it on Windows.</exception>
    void DeleteFile(string path);

    /// <summary>
    ///   Creates a hard link: a second name for the same file, sharing its
    ///   contents and its <see cref="StatIdentity"/>.
    /// </summary>
    /// <remarks>
    ///   Both paths must be on the same volume; compare
    ///   the <see cref="StatIdentity.VolumeID"/> from <see cref="GetStat"/>
    ///   of the source with that of the destination's folder first.
    /// </remarks>
    /// <param name="sourcePath">The existing file.</param>
    /// <param name="destinationPath">The new name for it.</param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="IOException">
    ///   The source does not exist or is a directory, the paths are on
    ///   different volumes, something already exists at the destination, the
    ///   link may not be made, or the file system cannot make hard links.
    /// </exception>
    void CreateHardLink(string sourcePath, string destinationPath);

    /// <summary>
    ///   Lists file paths beneath the given directory.
    /// </summary>
    /// <param name="directoryPath">The directory.</param>
    /// <param name="recursive">Whether to look in subdirectories too.</param>
    /// <param name="extensions">Only list files with these extensions.</param>
    /// <param name="filter">
    ///   Decides which entries to keep. A directory left out is not walked
    ///   either.
    /// </param>
    /// <param name="cancellationToken">Stops the listing.</param>
    /// <returns>The paths.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    string[] GetFilePaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, FileSystemEntryFilter? filter = null, CancellationToken cancellationToken = default);

    #endregion

    #region Directories

    /// <summary>
    ///   Checks whether a directory exists, treating a null or empty path as
    ///   absent.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><c>true</c> when a directory exists at the path.</returns>
    bool DirectoryExists(string? path);

    /// <summary>
    ///   Checks whether a directory holds nothing, reading only its first
    ///   entry.
    /// </summary>
    /// <param name="path">The directory.</param>
    /// <returns><c>true</c> when it exists and is empty.</returns>
    /// <exception cref="UnauthorizedAccessException">The directory may not be listed.</exception>
    /// <exception cref="IOException">It could not be listed for another reason.</exception>
    bool IsDirectoryEmpty(string path);

    /// <summary>
    ///   Gets a <see cref="DirectoryInfo"/> for the path, or <c>null</c> if the
    ///   directory does not exist.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The directory's info, or <c>null</c>.</returns>
    DirectoryInfo? GetDirectoryInfo(string? path);

    /// <summary>
    ///   Reads what the file system says about the directory at the given
    ///   path: <see cref="GetStat"/>, when it is a directory.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="followSymbolicLinks">
    ///   Whether a symbolic link answers for what it points to; when not, a
    ///   link gives <c>null</c>.
    /// </param>
    /// <returns>
    ///   The directory's info, or <c>null</c> when nothing is at the path or
    ///   it is not a directory.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">The entry may not be read.</exception>
    /// <exception cref="IOException">It could not be read for another reason.</exception>
    DirectoryStat? GetDirectoryStat(string? path, bool followSymbolicLinks = true)
        => GetStat(path, followSymbolicLinks) as DirectoryStat;

    /// <summary>
    ///   Creates the directory at the given path, including any missing
    ///   parents. Nothing happens when it exists.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">It may not be created.</exception>
    /// <exception cref="IOException">A file is at the path or at one of its parents, or it could not be created for another reason.</exception>
    void CreateDirectory(string path);

    /// <summary>
    ///   Deletes the directory at the given path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="recursive">Whether to delete what it holds too.</param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">It may not be deleted.</exception>
    /// <exception cref="DirectoryNotFoundException">No directory is at the path.</exception>
    /// <exception cref="IOException">It is not empty and <paramref name="recursive"/> is <c>false</c>, a file is at the path, or it could not be deleted for another reason.</exception>
    void DeleteDirectory(string path, bool recursive = false);

    /// <summary>
    ///   Moves a directory and all it holds to another path.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     On one volume it is renamed in a single step. Across volumes, where
    ///     a rename cannot go, what it holds is moved entry by entry and the
    ///     emptied source removed.
    ///   </para>
    ///   <para>
    ///     When a directory is already at the destination and
    ///     <paramref name="overwrite"/> is set, the two are merged: files
    ///     replace files of the same name, subdirectories merge into
    ///     subdirectories of the same name, and the emptied source is removed.
    ///     A merge that fails part way leaves what it moved moved.
    ///   </para>
    ///   <para>
    ///     Symbolic links inside are moved as links, never walked into. A move
    ///     that only changes the case of its name renames it even on a
    ///     case-insensitive file system, as <see cref="MoveFile"/> does.
    ///   </para>
    /// </remarks>
    /// <param name="sourcePath">The directory to move.</param>
    /// <param name="destinationPath">Where to move it.</param>
    /// <param name="overwrite">
    ///   Whether to merge into a directory already at the destination.
    ///   Otherwise one there throws an <see cref="IOException"/>.
    /// </param>
    /// <exception cref="ArgumentException">A path is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">Something in it may not be moved.</exception>
    /// <exception cref="DirectoryNotFoundException">No directory is at the source, or the destination's folder does not exist.</exception>
    /// <exception cref="IOException">
    ///   A directory is at the destination and <paramref name="overwrite"/> is
    ///   <c>false</c>, something that is not a directory is there, the
    ///   destination is inside the source, a file in the way could not be
    ///   replaced, or it could not be moved for another reason.
    /// </exception>
    void MoveDirectory(string sourcePath, string destinationPath, bool overwrite = false);

    /// <summary>
    ///   Lists directory paths beneath the given directory.
    /// </summary>
    /// <param name="directoryPath">The directory.</param>
    /// <param name="recursive">Whether to look in subdirectories too.</param>
    /// <param name="filter">
    ///   Decides which entries to keep. A directory left out is not walked
    ///   either.
    /// </param>
    /// <param name="cancellationToken">Stops the listing.</param>
    /// <returns>The paths.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    string[] GetDirectoryPaths(string directoryPath, bool recursive = false, FileSystemEntryFilter? filter = null, CancellationToken cancellationToken = default);

    #endregion

    #region Shared

    /// <summary>
    ///   Gets a <see cref="DirectoryInfo"/> or a <see cref="FileInfo"/> for the
    ///   entry at the path, whichever it is, or <c>null</c> if nothing is
    ///   there.
    /// </summary>
    /// <remarks>
    ///   A symbolic link is found itself, with its
    ///   <see cref="FileSystemInfo.LinkTarget"/> set: a
    ///   <see cref="DirectoryInfo"/> when it points to a directory, otherwise
    ///   a <see cref="FileInfo"/>, a dangling link included. These are .NET's
    ///   own objects, which read the entry lazily and again on
    ///   <see cref="FileSystemInfo.Refresh"/>. <see cref="GetStat"/> reads it
    ///   once, with its identity and link count.
    /// </remarks>
    /// <param name="path">The path.</param>
    /// <returns>The entry's info, or <c>null</c>.</returns>
    /// <exception cref="UnauthorizedAccessException">The entry may not be read.</exception>
    /// <exception cref="IOException">It could not be read for another reason.</exception>
    FileSystemInfo? GetFileSystemInfo(string? path);

    /// <summary>
    ///   Reads what the file system says about the entry at the given path, in
    ///   one call where the platform allows: its identity, link count, times
    ///   and, for a file, its size.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="followSymbolicLinks">
    ///   Whether a symbolic link answers for what it points to (a
    ///   <see cref="FileStat"/> or <see cref="DirectoryStat"/>, or
    ///   <c>null</c> when it points to nothing), or for itself (a
    ///   <see cref="SymbolicLinkStat"/>).
    /// </param>
    /// <returns>
    ///   The entry's info, or <c>null</c> when nothing is at the path or the
    ///   platform cannot tell. A pipe, a socket or a device is a
    ///   <see cref="SpecialFileStat"/>.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">The entry may not be read.</exception>
    /// <exception cref="IOException">It could not be read for another reason.</exception>
    FileSystemStat? GetStat(string? path, bool followSymbolicLinks = true);

    /// <summary>
    ///   Reads what the system says about the volume the entry at the given
    ///   path is on: its mount point and source, format, kind, whether it is
    ///   read-only, and its size and free space.
    /// </summary>
    /// <remarks>
    ///   The volume is the one the entry's <see cref="StatIdentity.VolumeID"/>
    ///   names, with symbolic links followed. On Linux it is read from
    ///   <c>/proc/self/mountinfo</c> and <c>statvfs</c>, elsewhere on Unix
    ///   from .NET's drive list and <c>statvfs</c>, on Windows from the volume
    ///   it is under.
    /// </remarks>
    /// <param name="path">A path on the volume.</param>
    /// <returns>The volume's info, or <c>null</c> when nothing is at the path.</returns>
    /// <exception cref="UnauthorizedAccessException">The entry may not be read.</exception>
    /// <exception cref="IOException">It or its volume could not be read for another reason.</exception>
    VolumeStat? GetVolumeStat(string? path);

    /// <summary>
    ///   Finds the mount a path is on, from the system's list of mounts alone,
    ///   without reading the volume, so a share whose server is gone cannot
    ///   hold it up.
    /// </summary>
    /// <remarks>
    ///   A symbolic link at the end of the path is followed first, since the
    ///   list only knows where volumes are mounted; links among its parent
    ///   folders are not. A Windows UNC path (<c>\\server\share\…</c>) is on a
    ///   network share of its own. <see cref="GetVolumeStat"/> reads the
    ///   volume itself, links and all.
    /// </remarks>
    /// <param name="path">The path.</param>
    /// <returns>The mount, or <c>null</c> when none holds the path.</returns>
    /// <exception cref="UnauthorizedAccessException">The list of mounts may not be read.</exception>
    /// <exception cref="IOException">The list of mounts could not be read for another reason.</exception>
    VolumeMount? GetVolumeMount(string? path);

    /// <summary>
    ///   Lists the mounted volumes, from the system's list of mounts, without
    ///   reading the volumes themselves.
    /// </summary>
    /// <remarks>
    ///   By default it lists what the folder browser offers: the system's
    ///   own mounts (<see cref="VolumeMount.IsSystem"/>) are left out, and on
    ///   Windows a drive that is not ready. On Linux it reads
    ///   <c>/proc/self/mountinfo</c> alone, so unless the stats are asked for a
    ///   share whose server is gone cannot hold it up; elsewhere it reads
    ///   .NET's drive list.
    /// </remarks>
    /// <param name="includeSystem">Whether to list the system's own mounts too.</param>
    /// <param name="includeStats">
    ///   Whether to read each volume too, giving a <see cref="VolumeStat"/>
    ///   with its sizes for each mount that can be read; one that cannot stays
    ///   a plain <see cref="VolumeMount"/>. Reading touches the volumes, so a
    ///   hard-mounted share whose server is gone makes the call wait.
    /// </param>
    /// <returns>The mounts, by mount point.</returns>
    /// <exception cref="UnauthorizedAccessException">The list of mounts may not be read.</exception>
    /// <exception cref="IOException">The list of mounts could not be read for another reason.</exception>
    IReadOnlyList<VolumeMount> GetVolumeMounts(bool includeSystem = false, bool includeStats = false);

    /// <summary>
    ///   Creates a symbolic link at the destination pointing to the source, a
    ///   file, a directory or another link.
    /// </summary>
    /// <remarks>
    ///   The link points to the source itself, not to where a link there
    ///   leads; pass <see cref="ResolveSymbolicLink"/>'s answer to link to the
    ///   end of a chain. The source must exist, a dangling link counting,
    ///   since Windows makes file and directory links apart. Windows only allows it to an administrator or with developer
    ///   mode turned on.
    /// </remarks>
    /// <param name="sourcePath">
    ///   The file or directory the link points to: absolute, or relative to
    ///   the destination's folder, and stored in the link as given.
    /// </param>
    /// <param name="destinationPath">Where to create the link.</param>
    /// <exception cref="FileNotFoundException">Nothing is at the source.</exception>
    /// <exception cref="UnauthorizedAccessException">The source may not be read.</exception>
    /// <exception cref="IOException">
    ///   Something already exists at the destination, or the link cannot be
    ///   made, such as on Windows without the right to make links.
    /// </exception>
    void CreateSymbolicLink(string sourcePath, string destinationPath);

    /// <summary>
    ///   Checks whether symbolic links can be made in a directory, by making a
    ///   file and a link to it there and removing both.
    /// </summary>
    /// <remarks>
    ///   It depends on the volume, so check each directory links go to: an
    ///   SMB share without symbolic link support, or a FAT or exFAT volume,
    ///   refuses them, and Windows only lets an administrator or a user with
    ///   developer mode on make them.
    /// </remarks>
    /// <param name="directoryPath">The directory.</param>
    /// <returns><c>true</c> when a link could be made there.</returns>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    /// <exception cref="DirectoryNotFoundException">No directory is at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The directory may not be written to, which says nothing about links.</exception>
    /// <exception cref="IOException">The test file could not be made, which says nothing about links.</exception>
    bool CanCreateSymbolicLinks(string directoryPath);

    /// <summary>
    ///   Follows the symbolic link at the given path to its target, and on
    ///   through any link the target is, to the final one.
    /// </summary>
    /// <remarks>
    ///   A relative target is read from the folder of the link holding it.
    ///   Only the last part of each path is followed; a link among its parent
    ///   folders is left to the operating system.
    /// </remarks>
    /// <param name="path">The path.</param>
    /// <param name="returnFinalTarget">
    ///   Whether to follow the whole chain, or stop at the link's own target.
    /// </param>
    /// <returns>
    ///   The target's full path, which need not exist, or <c>null</c> when
    ///   nothing is at the path or it is not a link.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">A link on the way may not be read.</exception>
    /// <exception cref="IOException">A link on the way could not be read for another reason.</exception>
    /// <exception cref="SymbolicLinkLoopException">
    ///   The chain leads back to a link already followed, or goes through
    ///   more than 40 links.
    /// </exception>
    string? ResolveSymbolicLink(string? path, bool returnFinalTarget = true);

    /// <summary>
    ///   Lists the file and directory paths beneath the given directory, the
    ///   walk <see cref="GetFilePaths"/> and <see cref="GetDirectoryPaths"/>
    ///   share. Subdirectories are walked in parallel, so the paths come in no
    ///   particular order, and one that cannot be read is logged and skipped.
    /// </summary>
    /// <param name="directoryPath">The directory.</param>
    /// <param name="recursive">Whether to look in subdirectories too.</param>
    /// <param name="extensions">
    ///   Only list files with these extensions. Directories are not filtered
    ///   by them.
    /// </param>
    /// <param name="filter">
    ///   Decides which entries to keep. A directory left out is not walked
    ///   either.
    /// </param>
    /// <param name="outputFiles">Whether to list files.</param>
    /// <param name="outputDirectories">Whether to list directories.</param>
    /// <param name="cancellationToken">Stops the listing.</param>
    /// <returns>The paths, or none when the directory does not exist.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    string[] GetFileSystemPaths(string directoryPath, bool recursive = false, IEnumerable<string>? extensions = null, FileSystemEntryFilter? filter = null, bool outputFiles = true, bool outputDirectories = true, CancellationToken cancellationToken = default);

    #endregion
}
