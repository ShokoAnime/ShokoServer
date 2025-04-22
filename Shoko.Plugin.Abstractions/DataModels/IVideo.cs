
using System.Collections.Generic;
using System.IO;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;

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
    /// The ED2K hash for the file.
    /// </summary>
    string ED2K { get; }

    /// <summary>
    /// The file size, in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// All video locations for the file.
    /// </summary>
    IReadOnlyList<IVideoFile> Locations { get; }

    /// <summary>
    /// The current release information for the video, if the video has an
    /// existing release associated with it. All recognized videos have a
    /// release associated with them.
    /// </summary>
    IReleaseInfo? ReleaseInfo { get; }

    /// <summary>
    /// All stored hashes for the video, including the ED2K which is mandatory
    /// and has a dedicated field/member above.
    /// </summary>
    IReadOnlyList<IHashDigest> Hashes { get; }

    /// <summary>
    /// The MediaInfo data for the file. This can be null, but it shouldn't be.
    /// </summary>
    IMediaInfo? MediaInfo { get; }

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

    /// <summary>
    /// Get the stream for the video, if any files are still available.
    /// </summary>
    Stream? GetStream();
}
