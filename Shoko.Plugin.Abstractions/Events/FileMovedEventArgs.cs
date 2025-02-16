using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a file is moved.
/// </summary>
public class FileMovedEventArgs : FileEventArgs
{
    /// <summary>
    /// The previous relative path of the file from the
    /// <see cref="PreviousImportFolder"/>'s base location.
    /// </summary>
    public string PreviousRelativePath { get; set; }

    /// <summary>
    /// The previous import folder that the file was in.
    /// </summary>
    public IImportFolder PreviousImportFolder { get; set; }

    /// <summary>
    /// The absolute path leading to the previous location of the file. Uses an OS dependent directory separator.
    /// </summary>
    public string PreviousPath => Path.Join(PreviousImportFolder.Path, PreviousRelativePath);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMovedEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="importFolder">Import folder.</param>
    /// <param name="previousRelativePath">Previous relative path to the file from the <paramref name="previousImportFolder"/>'s base location.</param>
    /// <param name="previousImportFolder">Previous import folder that the file was in.</param>
    /// <param name="fileInfo">File info.</param>
    /// <param name="videoInfo">Video info.</param>
    /// <param name="episodeInfo">Episode info.</param>
    /// <param name="animeInfo">Series info.</param>
    /// <param name="groupInfo">Group info.</param>
    public FileMovedEventArgs(string relativePath, IImportFolder importFolder, string previousRelativePath, IImportFolder previousImportFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo)
    {
        previousRelativePath = previousRelativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (previousRelativePath[0] != Path.DirectorySeparatorChar)
            previousRelativePath = Path.DirectorySeparatorChar + previousRelativePath;
        PreviousRelativePath = previousRelativePath;
        PreviousImportFolder = previousImportFolder;
    }
}
