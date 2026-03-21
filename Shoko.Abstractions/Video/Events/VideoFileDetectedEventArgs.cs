using System;
using System.IO;

namespace Shoko.Abstractions.Video.Events;

/// <summary>
/// Dispatched when a video file is detected.
/// </summary>
public class VideoFileDetectedEventArgs : EventArgs
{
    /// <summary>
    /// The relative path from the <see cref="ManagedFolder"/>'s root. Uses an OS dependent directory separator.
    /// </summary>
    public string RelativePath { get; }

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
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (relativePath[0] != Path.DirectorySeparatorChar)
            relativePath = Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        FileInfo = fileInfo;
        ManagedFolder = managedFolder;
    }
}
