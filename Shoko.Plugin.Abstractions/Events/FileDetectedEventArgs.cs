using System;
using System.IO;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a file is detected.
/// </summary>
public class FileDetectedEventArgs : EventArgs
{
    /// <summary>
    /// The relative path from the <see cref="ManagedFolder"/>'s root. Uses an OS dependent directory separator.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// The raw <see cref="System.IO.FileInfo"/> for the file. Don't go and accidentally delete the file now, okay?
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// The managed folder that the file is in.
    /// </summary>
    public IManagedFolder ManagedFolder { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDetectedEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">The relative path from the <see cref="ManagedFolder"/>'s root. Uses an OS dependent directory separator.</param>
    /// <param name="fileInfo">The raw <see cref="System.IO.FileInfo"/> for the file. Don't go and accidentally delete the file now, okay?</param>
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    public FileDetectedEventArgs(string relativePath, FileInfo fileInfo, IManagedFolder managedFolder)
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
