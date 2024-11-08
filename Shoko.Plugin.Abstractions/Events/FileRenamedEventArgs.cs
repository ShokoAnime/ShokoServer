using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a file is renamed.
/// </summary>
public class FileRenamedEventArgs : FileEventArgs
{
    /// <summary>
    /// The previous relative path of the file from the
    /// <see cref="FileEventArgs.ImportFolder"/>'s base location.
    /// </summary>
    public string PreviousRelativePath =>
        RelativePath.Substring(0, RelativePath.Length - FileName.Length) + PreviousFileName;

    /// <summary>
    /// The new file name after the rename operation.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The previous file name before the rename operation.
    /// </summary>
    public string PreviousFileName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenamedEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="importFolder">Import folder.</param>
    /// <param name="fileName">New file name.</param>
    /// <param name="previousFileName">Previous file name.</param>
    /// <param name="fileInfo">File info.</param>
    /// <param name="videoInfo">Video info.</param>
    /// <param name="episodeInfo">Episode info.</param>
    /// <param name="seriesInfo">Series info.</param>
    /// <param name="groupInfo">Group info.</param>
    public FileRenamedEventArgs(string relativePath, IImportFolder importFolder, string fileName, string previousFileName, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> seriesInfo, IEnumerable<IShokoGroup> groupInfo)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, seriesInfo, groupInfo)
    {
        FileName = fileName;
        PreviousFileName = previousFileName;
    }
}
