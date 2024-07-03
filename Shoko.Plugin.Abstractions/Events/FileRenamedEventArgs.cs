
using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions;

public class FileRenamedEventArgs : FileEventArgs
{
    /// <summary>
    /// The previous relative path of the file from the
    /// <see cref="PreviousImportFolder"/>'s base location.
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

    #region To-be-removed

    [Obsolete("Use FileName instead.")]
    public string NewFileName => FileName;

    [Obsolete("Use PreviousFileName instead.")]
    public string OldFileName => PreviousFileName;

    #endregion

    public FileRenamedEventArgs(string relativePath, IImportFolder importFolder, string fileName, string previousFileName, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IEpisode> episodeInfo, IEnumerable<ISeries> animeInfo, IEnumerable<IGroup> groupInfo)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo)
    {
        FileName = fileName;
        PreviousFileName = previousFileName;
    }
}
