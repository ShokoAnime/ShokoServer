using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

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
    /// The id of the provider where the release can be found, used to identify
    /// which provider found the release.
    /// </summary>
    string ProviderID { get; }

    /// <summary>
    /// An absolute URI for where to find the information, if available from the provider.
    /// Can be a http://, https:// or file:// URI.
    /// </summary>
    public string? ProviderURI { get; set; }

    /// <summary>
    /// Release revision number. Will be increased each time the <see cref="Group"/> releases a new version for the same release.
    /// The value is not guaranteed to be unique.
    /// </summary>
    int Revision { get; }

    /// <summary>
    /// A comment about the release info.
    /// </summary>
    string? Comment { get; }

    /// <summary>
    /// The original name of the file, if it's known by the release info provider.
    /// </summary>
    string? OriginalFilename { get; }

    /// <summary>
    /// Indicates that the release is censored or decensored. For most releases
    /// this will be <c>null</c>.
    /// </summary>
    bool? IsCensored { get; }

    /// <summary>
    /// Indicates that the released file is corrupted.
    /// </summary>
    public bool IsCorrupted { get; }

    /// <summary>
    /// The source of the release. What the video file was created from.
    /// </summary>
    ReleaseSource Source { get; }

    /// <summary>
    /// The release group associated with the release, if known. Can be omitted.
    /// </summary>
    IReleaseGroup? Group { get; }

    /// <summary>
    /// Remote media inforation about the file, if available from the provider.
    /// </summary>
    IReleaseMediaInfo? MediaInfo { get; }

    /// <summary>
    /// All video to episode cross-references included in this release.
    /// </summary>
    IReadOnlyList<IReleaseVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// When the video was released, according to the provider. May or may not
    /// be accurate, but at least it's something. Can be <c>null</c> if not
    /// known.
    /// </summary>
    DateOnly? ReleasedAt { get; }

    /// <summary>
    /// When the release information was last updated by the provider. Can be
    /// <c>null</c> if not applicable.
    /// </summary>
    DateTime? LastUpdatedAt { get; }

    /// <summary>
    /// When the release information was locally saved in Shoko for the first time.
    /// </summary>
    DateTime CreatedAt { get; }
}
