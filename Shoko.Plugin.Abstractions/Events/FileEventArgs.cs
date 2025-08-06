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
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    /// <param name="fileInfo">The <see cref="IVideoFile"/> information for the file.</param>
    /// <param name="videoInfo">The <see cref="IVideo"/> information for the file.</param>
    /// <remarks>
    /// This constructor is used to create an instance of <see cref="FileEventArgs"/> when the relative path of the file is not known.
    /// </remarks>
    public FileEventArgs(IManagedFolder managedFolder, IVideoFile fileInfo, IVideo videoInfo)
        : this(fileInfo.Path[managedFolder.Path.Length..], managedFolder, fileInfo, videoInfo) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    /// <param name="fileInfo">The <see cref="IVideoFile"/> information for the file.</param>
    /// <param name="videoInfo">The <see cref="IVideo"/> information for the file.</param>
    public FileEventArgs(string relativePath, IManagedFolder managedFolder, IVideoFile fileInfo, IVideo videoInfo)
    {
        relativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (relativePath[0] != Path.DirectorySeparatorChar)
            relativePath = Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        ManagedFolder = managedFolder;
        File = fileInfo;
        Video = videoInfo;
        Episodes = Video.Episodes;
        Series = Video.Series;
        Groups = Video.Groups;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileEventArgs"/> class.
    /// </summary>
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    /// <param name="fileInfo">The <see cref="IVideoFile"/> information for the file.</param>
    /// <param name="videoInfo">The <see cref="IVideo"/> information for the file.</param>
    /// <param name="episodeInfo">The collection of <see cref="IShokoEpisode"/> information for the file.</param>
    /// <param name="animeInfo">The collection of <see cref="IShokoSeries"/> information for the file.</param>
    /// <param name="groupInfo">The collection of <see cref="IShokoGroup"/> information for the file.</param>
    /// <remarks>
    /// This constructor is intended to be used when the relative path is not known.
    /// It is recommended to use the other constructor whenever possible.
    /// </remarks>
    public FileEventArgs(IManagedFolder managedFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
        : this(fileInfo.Path[managedFolder.Path.Length..], managedFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    /// <param name="fileInfo">The <see cref="IVideoFile"/> information for the file.</param>
    /// <param name="videoInfo">The <see cref="IVideo"/> information for the file.</param>
    /// <param name="episodeInfo">The collection of <see cref="IShokoEpisode"/> information for the file.</param>
    /// <param name="animeInfo">The collection of <see cref="IShokoSeries"/> information for the file.</param>
    /// /// <param name="groupInfo">The collection of <see cref="IShokoGroup"/> information for the file.</param>
    public FileEventArgs(string relativePath, IManagedFolder managedFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
    {
        relativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (relativePath[0] != Path.DirectorySeparatorChar)
            relativePath = Path.DirectorySeparatorChar + relativePath;
        RelativePath = relativePath;
        ManagedFolder = managedFolder;
        File = fileInfo;
        Video = videoInfo;
        Episodes = episodeInfo.ToArray();
        Series = animeInfo.ToArray();
        Groups = groupInfo.ToArray();
    }

    /// <summary>
    /// The relative path from the <see cref="ManagedFolder"/>'s root.
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
    /// The managed folder that the file is located in.
    /// </summary>
    public IManagedFolder ManagedFolder { get; }

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
