using System.Collections.Generic;
using System.IO;
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
    /// <see cref="FileEventArgs.ManagedFolder"/>'s base location.
    /// </summary>
    public string PreviousRelativePath =>
        RelativePath[..^FileName.Length] + PreviousFileName;

    /// <summary>
    /// The new file name after the rename operation.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The previous file name before the rename operation.
    /// </summary>
    public string PreviousFileName { get; }

    /// <summary>
    /// The absolute path leading to the previous location of the file. Uses an OS dependent directory separator.
    /// </summary>
    public string PreviousPath => Path.Join(ManagedFolder.Path, PreviousRelativePath);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenamedEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    /// <param name="fileName">New file name.</param>
    /// <param name="previousFileName">Previous file name.</param>
    /// <param name="fileInfo">File info.</param>
    /// <param name="videoInfo">Video info.</param>
    /// <param name="episodeInfo">Episode info.</param>
    /// <param name="seriesInfo">Series info.</param>
    /// <param name="groupInfo">Group info.</param>
    public FileRenamedEventArgs(string relativePath, IManagedFolder managedFolder, string fileName, string previousFileName, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> seriesInfo, IEnumerable<IShokoGroup> groupInfo)
        : base(relativePath, managedFolder, fileInfo, videoInfo, episodeInfo, seriesInfo, groupInfo)
    {
        FileName = fileName;
        PreviousFileName = previousFileName;
    }
}
