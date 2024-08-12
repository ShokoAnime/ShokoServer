using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

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

    #region To-be-removed

    [Obsolete("Use ImportFolder instead.")]
    public IImportFolder NewImportFolder => ImportFolder;

    [Obsolete("Use RelativePath instead.")]
    public string NewRelativePath => RelativePath;

    [Obsolete("Use PreviousImportFolder instead.")]
    public IImportFolder OldImportFolder => PreviousImportFolder;

    [Obsolete("Use PreviousRelativePath instead.")]
    public string OldRelativePath => PreviousRelativePath;

    #endregion

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
