using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Plugin.Abstractions;

public class RenameEventArgs : CancelEventArgs
{
    /// <summary>
    /// The contents of the renamer scrpipt
    /// </summary>
    public IRenameScript Script { get; }

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

    public RenameEventArgs(IRenameScript script, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IEpisode> episodeInfo, IEnumerable<IAnime> animeInfo, IEnumerable<IGroup> groupInfo)
    {
        Script = script;
        FileInfo = fileInfo;
        VideoInfo = videoInfo;
        EpisodeInfo = episodeInfo.ToArray();
        AnimeInfo = animeInfo.ToArray();
        GroupInfo = groupInfo.ToArray();
    }
}
