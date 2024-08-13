
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Video.
/// </summary>
public interface IVideo : IMetadata<int>
{
    /// <summary>
    /// The earliest known local file name of the video.
    /// </summary>
    string? EarliestKnownName { get; }

    /// <summary>
    /// The file size, in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// All video locations for the file.
    /// </summary>
    IReadOnlyList<IVideoFile> Locations { get; }

    /// <summary>
    /// The AniDB File Info. This will be null for manual links, which can reliably be used to tell if it was manually linked.
    /// </summary>
    IAniDBFile? AniDB { get; }

    /// <summary>
    /// The Relevant Hashes for a file. CRC should be the only thing used here, but clever uses of the API could use the others.
    /// </summary>
    IHashes Hashes { get; }

    /// <summary>
    /// The MediaInfo data for the file. This can be null, but it shouldn't be.
    /// </summary>
    IMediaContainer? MediaInfo { get; }

    /// <summary>
    /// All cross-references linked to the video.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// All episodes linked to the video.
    /// </summary>
    IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// All shows linked to the show.
    /// </summary>
    IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// Information about the group
    /// </summary>
    IReadOnlyList<IShokoGroup> Groups { get; }
}
