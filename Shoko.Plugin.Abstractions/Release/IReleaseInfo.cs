using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release info interface.
/// </summary>
public interface IReleaseInfo
{
    /// <summary>
    /// The ID of the release by the provider, if available from the provider.
    /// </summary>
    string? ID { get; }

    /// <summary>
    /// The name of the provider which found the release.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// An absolute URI for where to find information about the release online,
    /// if available from the provider. Can be a http://, https:// or file://
    /// URI.
    /// </summary>
    string? ReleaseURI { get; }

    /// <summary>
    /// Release revision number. Might be increased each time the
    /// <see cref="Group"/> releases a new version for the same release.
    /// The value is not guaranteed to be unique.
    /// </summary>
    int Revision { get; }

    /// <summary>
    /// File size in bytes, if available from the provider.
    /// </summary>
    long? FileSize { get; }

    /// <summary>
    /// Comment about the release info, if available from the provider.
    /// </summary>
    string? Comment { get; }

    /// <summary>
    /// The original name of the file, if it's known by the release info
    /// provider.
    /// </summary>
    string? OriginalFilename { get; }

    /// <summary>
    /// Indicates that the release is censored or de-censored. For most releases
    /// this will be <c>null</c>.
    /// </summary>
    bool? IsCensored { get; }

    /// <summary>
    /// Indicates that the release is chaptered, if it's known by the release
    /// info provider.
    /// </summary>
    bool? IsChaptered { get; }

    /// <summary>
    /// Indicates that the release is an OP/ED without credits, if it's known by
    /// the release info provider.
    /// </summary>
    bool? IsCreditless { get; }

    /// <summary>
    /// Indicates that the released file is corrupted.
    /// </summary>
    bool IsCorrupted { get; }

    /// <summary>
    /// The source of the release. What the video file was created from.
    /// </summary>
    ReleaseSource Source { get; }

    /// <summary>
    /// The release group associated with the release, if known. Can be omitted.
    /// </summary>
    IReleaseGroup? Group { get; }

    /// <summary>
    /// Override hashes for the file, if available from the provider.
    /// </summary>
    IReadOnlyList<IHashDigest>? Hashes { get; }

    /// <summary>
    /// Remote media information about the file, if available from the provider.
    /// </summary>
    IReleaseMediaInfo? MediaInfo { get; }

    /// <summary>
    /// All video to episode cross-references included in this release.
    /// </summary>
    IReadOnlyList<IReleaseVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// Metadata about the release from the provider or user, if available.
    /// </summary>
    string? Metadata { get; }

    /// <summary>
    /// When the video was released, according to the provider. May or may not
    /// be accurate, but at least it's something. Can be <c>null</c> if not
    /// known.
    /// </summary>
    DateOnly? ReleasedAt { get; }

    /// <summary>
    /// When the release information was last updated locally.
    /// </summary>
    DateTime LastUpdatedAt { get; }

    /// <summary>
    /// When the release information was locally saved in Shoko for the first
    /// time.
    /// </summary>
    DateTime CreatedAt { get; }
}
