using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// The event arguments for a Move event. It can be cancelled by setting Cancel to true or skipped by not setting the result parameters.
/// </summary>
public class MoveEventArgs : CancelEventArgs
{
    /// <summary>
    /// The renamer script contents
    /// </summary>
    public IRenameScript Script { get; }

    /// <summary>
    /// The available import folders to choose as a destination. You can set the <see cref="DestinationImportFolder"/> to one of these.
    /// If a Folder has <see cref="DropFolderType.Excluded"/> set, then it won't be in this list.
    /// </summary>
    public IReadOnlyList<IImportFolder> AvailableFolders { get; }

    /// <summary>
    /// Information about the file itself, such as MediaInfo
    /// </summary>
    public IVideoFile File { get; }

    /// <summary>
    /// Information about the video.
    /// </summary>
    public IVideo Video { get; }

    /// <summary>
    /// Information about the shoko episodes.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// Information about the shoko series.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// Information about the shoko groups.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; }

    public MoveEventArgs(IRenameScript script, IEnumerable<IImportFolder> availableFolders, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
    {
        Script = script;
        AvailableFolders = availableFolders.ToArray();
        File = fileInfo;
        Video = videoInfo;
        Episodes = episodeInfo.ToArray();
        Series = animeInfo.ToArray();
        Groups = groupInfo.ToArray();
    }
}

