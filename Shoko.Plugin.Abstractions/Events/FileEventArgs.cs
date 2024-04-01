using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
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
        FileInfo = fileInfo;
        VideoInfo = videoInfo;
        EpisodeInfo = VideoInfo.EpisodeInfo;
        AnimeInfo = VideoInfo.SeriesInfo
                        .OfType<IAnime>()
                        .ToArray();
        GroupInfo = VideoInfo.GroupInfo;
    }

    public FileEventArgs(IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IEpisode> episodeInfo, IEnumerable<IAnime> animeInfo, IEnumerable<IGroup> groupInfo)
        : this(fileInfo.Path.Substring(importFolder.Path.Length), importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo) { }

    public FileEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IEpisode> episodeInfo, IEnumerable<IAnime> animeInfo, IEnumerable<IGroup> groupInfo)
    {
        relativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (relativePath[0] != Path.DirectorySeparatorChar)
            relativePath = Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        ImportFolder = importFolder;
        FileInfo = fileInfo;
        VideoInfo = videoInfo;
        EpisodeInfo = episodeInfo.ToArray();
        AnimeInfo = animeInfo.ToArray();
        GroupInfo = groupInfo.ToArray();
    }

    /// <summary>
    /// The relative path from the <see cref="ImportFolder"/>'s root. Uses an OS dependent directory seperator.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Information about the video and video file location, such as ids, media info, hashes, etc..
    /// </summary>
    public IVideoFile FileInfo { get; }

    /// <summary>
    /// Information about the video.
    /// </summary>
    public IVideo VideoInfo { get; }

    /// <summary>
    /// The import folder that the file is in.
    /// </summary>
    public IImportFolder ImportFolder { get; }

    /// <summary>
    /// Episodes Linked to the file.
    /// </summary>
    public IReadOnlyList<IEpisode> EpisodeInfo { get; }

    /// <summary>
    /// Information about the Anime, such as titles
    /// </summary>
    public IReadOnlyList<IAnime> AnimeInfo { get; }

    /// <summary>
    /// Information about the group
    /// </summary>
    public IReadOnlyList<IGroup> GroupInfo { get; }
}
