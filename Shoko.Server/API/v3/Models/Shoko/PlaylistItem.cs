using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
///   A single entry in a generated playlist. Groups one or more episodes with
///   their associated video files.
/// </summary>
public class PlaylistItem
{
    /// <summary>
    ///   The primary episode for this playlist entry. Use
    ///   <see cref="AdditionalEpisodes"/> for episodes that share the same
    ///   video file (e.g. merged multi-episode files).
    /// </summary>
    [Required]
    public PlaylistEpisode Episode { get; }

    /// <summary>
    ///   Additional episodes grouped under the same video file(s). This occurs
    ///   when Shoko automatically merges consecutive playlist entries that
    ///   reference the same video file.
    /// </summary>
    [Required]
    public IReadOnlyList<PlaylistEpisode> AdditionalEpisodes { get; }

    /// <summary>
    ///   The video files that constitute this playlist entry. Multiple parts
    ///   indicate a multi-part release (e.g. split across multiple files).
    /// </summary>
    [Required]
    public IReadOnlyList<File> Parts { get; }

    /// <summary>
    ///   Initializes a new <see cref="PlaylistItem"/>.
    /// </summary>
    /// <param name="episodes">
    ///   Episodes for this playlist entry. The first is the primary episode.
    /// </param>
    /// <param name="files">
    ///   Video files for this playlist entry.
    /// </param>
    public PlaylistItem(IReadOnlyList<PlaylistEpisode> episodes, IReadOnlyList<File> files)
    {
        Episode = episodes[0];
        AdditionalEpisodes = episodes.Skip(1).ToList();
        Parts = files;
    }
}

