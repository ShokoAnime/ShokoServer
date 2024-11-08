using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Playlist item.
/// </summary>
public class PlaylistItem
{
    /// <summary>
    /// The main episode for the playlist item.
    /// </summary>
    public Episode Episode { get; }

    /// <summary>
    /// Any additional episodes for the playlist item, if any.
    /// </summary>
    public IReadOnlyList<Episode> AdditionalEpisodes { get; }

    /// <summary>
    /// All file parts for the playlist item.
    /// </summary>
    public IReadOnlyList<File> Parts { get; }

    /// <summary>
    /// Initializes a new <see cref="PlaylistItem"/>.
    /// </summary>
    /// <param name="episodes">Episodes.</param>
    /// <param name="files">Files.</param>
    public PlaylistItem(IReadOnlyList<Episode> episodes, IReadOnlyList<File> files)
    {
        Episode = episodes[0];
        AdditionalEpisodes = episodes.Skip(1).ToList();
        Parts = files;
    }
}

