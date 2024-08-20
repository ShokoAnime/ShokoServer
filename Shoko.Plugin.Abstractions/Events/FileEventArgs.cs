using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a file event is needed. This is shared across a few events,
/// and also the base class for more specific events.
/// </summary>
public class FileEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileEventArgs"/> class.
    /// </summary>
    /// <param name="importFolder">The import folder that the file is in.</param>
    /// <param name="fileInfo">The raw <see cref="System.IO.FileInfo"/> for the file.</param>
    /// <param name="videoInfo">The video information for the file.</param>
    /// <remarks>
    /// This constructor is used to create an instance of <see cref="FileEventArgs"/> when the relative path of the file is not known.
    /// </remarks>
    public FileEventArgs(IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo)
        : this(fileInfo.Path.Substring(importFolder.Path.Length), importFolder, fileInfo, videoInfo) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="importFolder">Import folder.</param>
    /// <param name="fileInfo">File info.</param>
    /// <param name="videoInfo">Video info.</param>
    public FileEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo)
    {
        relativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (relativePath[0] != Path.DirectorySeparatorChar)
            relativePath = Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        ImportFolder = importFolder;
        File = fileInfo;
        Video = videoInfo;
        Episodes = Video.Episodes;
        Series = Video.Series;
        Groups = Video.Groups;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileEventArgs"/> class.
    /// </summary>
    /// <param name="importFolder">The import folder that the file is in.</param>
    /// <param name="fileInfo">The raw <see cref="System.IO.FileInfo"/> for the file.</param>
    /// <param name="videoInfo">The <see cref="IVideo"/> info for the file.</param>
    /// <param name="episodeInfo">The collection of <see cref="IShokoEpisode"/> info for the file.</param>
    /// <param name="animeInfo">The collection of <see cref="IShokoSeries"/> info for the file.</param>
    /// <param name="groupInfo">The collection of <see cref="IShokoGroup"/> info for the file.</param>
    /// <remarks>
    /// This constructor is intended to be used when the relative path is not known.
    /// It is recommended to use the other constructor whenever possible.
    /// </remarks>
    public FileEventArgs(IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
        : this(fileInfo.Path.Substring(importFolder.Path.Length), importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="importFolder">Import folder.</param>
    /// <param name="fileInfo">File info.</param>
    /// <param name="videoInfo">Video info.</param>
    /// <param name="episodeInfo">Episode info.</param>
    /// <param name="animeInfo">Series info.</param>
    /// /// <param name="groupInfo">Group info.</param>
    public FileEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
    {
        relativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (relativePath[0] != Path.DirectorySeparatorChar)
            relativePath = Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        ImportFolder = importFolder;
        File = fileInfo;
        Video = videoInfo;
        Episodes = episodeInfo.ToArray();
        Series = animeInfo.ToArray();
        Groups = groupInfo.ToArray();
    }

    /// <summary>
    /// The relative path from the <see cref="ImportFolder"/>'s root.
    /// Uses an OS dependent directory separator.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// The video file location.
    /// </summary>
    public IVideoFile File { get; }

    /// <summary>
    /// The video.
    /// </summary>
    public IVideo Video { get; }

    /// <summary>
    /// The import folder that the file is located in.
    /// </summary>
    public IImportFolder ImportFolder { get; }

    /// <summary>
    /// Episodes linked to the video.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// Series linked to the video.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// Groups linked to the series that are in turn linked to the video.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; }
}
