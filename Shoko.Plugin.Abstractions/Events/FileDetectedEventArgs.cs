using System;
using System.IO;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions;

public class FileDetectedEventArgs : EventArgs
{
    /// <summary>
    /// The relative path from the <see cref="ImportFolder"/>'s root. Uses an OS dependent directory separator.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// The raw <see cref="System.IO.FileInfo"/> for the file. Don't go and accidentally delete the file now, okay?
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// The import folder that the file is in.
    /// </summary>
    public IImportFolder ImportFolder { get; }

    public FileDetectedEventArgs(string relativePath, FileInfo fileInfo, IImportFolder importFolder)
    {
        relativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (relativePath[0] != Path.DirectorySeparatorChar)
            relativePath = Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        FileInfo = fileInfo;
        ImportFolder = importFolder;
    }
}
