using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions;

public class FileEventArgs : EventArgs
{
    public FileEventArgs(IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo)
        : this(fileInfo.Path.Substring(importFolder.Path.Length), importFolder, fileInfo, videoInfo) { }

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

    public FileEventArgs(IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
        : this(fileInfo.Path.Substring(importFolder.Path.Length), importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo) { }

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
    /// The relative path from the <see cref="ImportFolder"/>'s root. Uses an OS dependent directory seperator.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Information about the video and video file location, such as ids, media info, hashes, etc..
    /// </summary>
    public IVideoFile File { get; }

    /// <summary>
    /// Information about the video.
    /// </summary>
    public IVideo Video { get; }

    /// <summary>
    /// The import folder that the file is in.
    /// </summary>
    public IImportFolder ImportFolder { get; }

    /// <summary>
    /// Episodes Linked to the file.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// Information about the Anime, such as titles
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// Information about the group
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; }
}
