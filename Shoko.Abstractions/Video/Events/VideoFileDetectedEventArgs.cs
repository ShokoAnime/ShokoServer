using System;
using System.IO;

namespace Shoko.Abstractions.Video.Events;

/// <summary>
/// Dispatched when a video file is detected.
/// </summary>
public class VideoFileDetectedEventArgs : EventArgs
{
    /// <summary>
    /// The relative path from the <see cref="ManagedFolder"/>'s root.
    /// Uses an OS dependent directory separator, and always starts with one, so
    /// join it to a folder with <c>Path.Join</c> rather than <c>Path.Combine</c>, which
    /// discards the folder when the second path is rooted.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// The absolute path to the video file, as it was when the file was detected.
    /// </summary>
    /// <remarks>
    /// The event is raised before any record exists for the path, so this is the only place the
    /// full path is recorded as a fact of the event rather than derived later from
    /// <see cref="ManagedFolder"/> and <see cref="RelativePath"/>, which may have changed by then.
    /// </remarks>
    public string Path { get; }

    /// <summary>
    /// The raw <see cref="System.IO.FileInfo"/> for the video file.
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// The managed folder that the video file is in.
    /// </summary>
    public IManagedFolder ManagedFolder { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoFileDetectedEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">The relative path from the <see cref="ManagedFolder"/>'s root. Uses an OS dependent directory separator.</param>
    /// <param name="fileInfo">The raw <see cref="System.IO.FileInfo"/> for the video file.</param>
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    public VideoFileDetectedEventArgs(string relativePath, FileInfo fileInfo, IManagedFolder managedFolder)
    {
        relativePath = relativePath
            .Replace('/', System.IO.Path.DirectorySeparatorChar)
            .Replace('\\', System.IO.Path.DirectorySeparatorChar);
        if (relativePath[0] != System.IO.Path.DirectorySeparatorChar)
            relativePath = System.IO.Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        Path = fileInfo.FullName;
        FileInfo = fileInfo;
        ManagedFolder = managedFolder;
    }
}
