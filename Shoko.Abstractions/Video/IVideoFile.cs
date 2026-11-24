using System.IO;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;

namespace Shoko.Abstractions.Video;

/// <summary>
/// Video file location.
/// </summary>
public interface IVideoFile
{
    /// <summary>
    /// The video file location id.
    /// </summary>
    int ID { get; }

    /// <summary>
    ///   The <see cref="ID"/> of the primary location of the file group this
    ///   location is part of: its own ID when it is the primary, or alone.
    ///   Locations that are the same file, hard links of each other and
    ///   symbolic links with the file they point to, share it. As last seen on
    ///   disk; see <see cref="VideoFileGroup"/>.
    /// </summary>
    int PrimaryFileID { get; }

    /// <summary>
    /// The video id.
    /// </summary>
    int VideoID { get; }

    /// <summary>
    /// The managed folder id.
    /// </summary>
    int ManagedFolderID { get; }

    /// <summary>
    ///   What the file system says about the file now: its identity, link
    ///   count, times and size, read in one call. <c>null</c> when it does not
    ///   exist, is not a file, or cannot be read.
    /// </summary>
    /// <remarks>
    ///   Read anew on every access, so keep the result rather than asking
    ///   twice. Two locations are the same file, hard links of each other, when
    ///   their <see cref="FileSystemStat.Identity"/> is equal, read at the same
    ///   time. See <see cref="IFileSystem.GetStat"/>.
    /// </remarks>
    FileStat? Stat { get; }

    /// <summary>
    ///   What the file system says about the location itself when it is a
    ///   symbolic link, read without following it, with the stat of what it
    ///   points to (<see cref="SymbolicLinkStat.TargetStat"/>); <c>null</c>
    ///   when it is not a link, or cannot be read. Read anew on every access.
    /// </summary>
    SymbolicLinkStat? LinkStat { get; }

    /// <summary>
    /// True if the file currently exists on disk and is usable by Shoko.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///   Whether the location is kept: its file group is never a duplicate,
    ///   and is never removed as one. Set with
    ///   <see cref="Services.IVideoService.SetVideoFileKept"/>.
    /// </summary>
    bool IsKept { get; }

    /// <summary>
    ///   Whether the location is a symbolic link, as last seen on disk: when
    ///   it was found, moved or scanned.
    /// </summary>
    bool IsSymbolicLink { get; }

    /// <summary>
    ///   The absolute path the location's symbolic link leads to, through
    ///   every link on the way, as last seen on disk; <c>null</c> when it is
    ///   not a link.
    /// </summary>
    string? SymbolicLinkTarget { get; }

    /// <summary>
    /// The file name.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// The absolute path leading to the location of the file. Uses an OS dependent directory separator.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// The relative path from the <see cref="ManagedFolder"/> to the location of the file. Will always use forward slash as a directory
    /// separator, and will always start with a leading slash.
    /// <br/>
    /// E.g.
    /// "C:\absolute\relative\path.ext" becomes "/relative/path.ext" if "C:\absolute" is the managed folder.
    /// or
    /// "/absolute/relative/path.ext" becomes "/relative/path.ext" if "/absolute" is the managed folder.
    /// </summary>
    string RelativePath { get; }

    /// <summary>
    /// The file size, in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Get the video tied to the video file location.
    /// </summary>
    /// <value></value>
    IVideo Video { get; }

    /// <summary>
    /// The managed folder tied to the video file location.
    /// </summary>
    IManagedFolder ManagedFolder { get; }

    /// <summary>
    /// Get the stream for the video file, if the file is still available.
    /// </summary>
    Stream? GetStream();
}
