using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Video.Release.Management;

/// <summary>
/// A single file within a <see cref="ReleaseOverride"/>.
/// </summary>
public class ReleaseOverrideFile
{
    /// <summary>
    /// The file location.
    /// </summary>
    public required IVideoFile File { get; init; }

    /// <summary>
    /// Release version (1 = original, 2+ = updated).
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// True if the file has chapter markers; null if chapter data is absent.
    /// </summary>
    public bool? IsChaptered { get; init; }

    /// <summary>
    /// Number of subtitle/text tracks in the file.
    /// </summary>
    public int SubtitleStreamCount { get; init; }

    /// <summary>
    /// All (EpisodeType, EpisodeNumber) pairs covered by this file.
    /// </summary>
    public IReadOnlySet<(EpisodeType Type, int Number)> Episodes { get; init; }
        = new HashSet<(EpisodeType, int)>();
}
