using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
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
    public IVideoFile FileInfo { get; }

    /// <summary>
    /// Information about the video.
    /// </summary>
    public IVideo VideoInfo { get; }

    /// <summary>
    /// Information about the episode, such as titles
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

    public MoveEventArgs(IRenameScript script, IEnumerable<IImportFolder> availableFolders, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IEpisode> episodeInfo, IEnumerable<IAnime> animeInfo, IEnumerable<IGroup> groupInfo)
    {
        Script = script;
        AvailableFolders = availableFolders.ToArray();
        FileInfo = fileInfo;
        VideoInfo = videoInfo;
        EpisodeInfo = episodeInfo.ToArray();
        AnimeInfo = animeInfo.ToArray();
        GroupInfo = groupInfo.ToArray();
    }
}

