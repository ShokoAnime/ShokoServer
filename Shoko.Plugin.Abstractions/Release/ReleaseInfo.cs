using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release info.
/// </summary>
public class ReleaseInfo
{
    /// <summary>
    /// The ID of the release by the provider, if available from the provider.
    /// </summary>
    public string? ID { get; set; }

    /// <inheritdoc />
    public string? ProviderName { get; set; }

    /// <summary>
    /// An URL for where to find the information online, if available from the provider.
    /// </summary>
    public string? ReleaseURI { get; set; }

    /// <summary>
    /// Release revision number. Will be increased each time the <see cref="Group"/> releases a new version for the same release.
    /// The value is not guaranteed to be unique.
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// File size in bytes, if available from the provider.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// A comment about the release info.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// The original name of the file, if it's known by the release info provider.
    /// </summary>
    public string? OriginalFilename { get; set; }

    /// <summary>
    /// Indicates that the release is censored or de-censored. For most releases
    /// this will be <c>null</c>.
    /// </summary>
    public bool? IsCensored { get; set; }

    /// <summary>
    /// Indicates that the released file is corrupted.
    /// </summary>
    public bool IsCorrupted { get; set; }

    /// <summary>
    /// Indicates that the release is chaptered, if it's known by the release
    /// info provider.
    /// </summary>
    public bool? IsChaptered { get; set; }

    /// <summary>
    /// The source of the release. What the video file was created from.
    /// </summary>
    public ReleaseSource Source { get; set; }

    /// <summary>
    /// The release group associated with the release, if known. Can be omitted.
    /// </summary>
    public ReleaseGroup? Group { get; set; }

    /// <summary>
    /// Override hashes for the file, if available from the provider.
    /// </summary>
    public ReleaseHashes? Hashes { get; set; }

    /// <summary>
    /// Remote media information about the file, if available from the provider.
    /// </summary>
    public ReleaseMediaInfo? MediaInfo { get; set; }

    /// <summary>
    /// All video to episode cross-references included in this release.
    /// </summary>
    public List<ReleaseVideoCrossReference> CrossReferences { get; set; } = [];

    /// <summary>
    /// When the video was released, according to the provider. May or may not
    /// be accurate, but at least it's something. Can be <c>null</c> if not
    /// known.
    /// </summary>
    public DateOnly? ReleasedAt { get; set; }

    /// <summary>
    /// When the release information was locally saved in Shoko for the first time.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Constructs a new <see cref="ReleaseInfo"/> instance.
    /// </summary>
    public ReleaseInfo()
    {
        CreatedAt = DateTime.Now;
    }

    /// <summary>
    /// Constructs a new <see cref="ReleaseInfo"/> instance from a
    /// <see cref="ReleaseInfo"/>.
    /// </summary>
    /// <param name="info">The <see cref="IReleaseInfo"/> to construct from.</param>
    public ReleaseInfo(ReleaseInfo info)
    {
        ID = info.ID;
        Revision = info.Revision;
        Comment = info.Comment;
        OriginalFilename = info.OriginalFilename;
        IsCensored = info.IsCensored;
        Source = info.Source;
        Group = info.Group is not null ? new(info.Group) : null;
        Hashes = info.Hashes is not null ? new(info.Hashes) : null;
        MediaInfo = info.MediaInfo is not null ? new(info.MediaInfo) : null;
        CrossReferences = info.CrossReferences.Select(xref => new ReleaseVideoCrossReference(xref)).ToList();
        ReleasedAt = info.ReleasedAt;
        CreatedAt = info.CreatedAt;
    }

    /// <summary>
    /// Constructs a new <see cref="ReleaseInfo"/> instance from a
    /// <see cref="IReleaseInfo"/>.
    /// </summary>
    /// <param name="info">The <see cref="IReleaseInfo"/> to construct from.</param>
    public ReleaseInfo(IReleaseInfo info)
    {
        ID = info.ID;
        ProviderName = info.ProviderName;
        Revision = info.Revision;
        Comment = info.Comment;
        OriginalFilename = info.OriginalFilename;
        IsCensored = info.IsCensored;
        Source = info.Source;
        Group = info.Group is not null ? new(info.Group) : null;
        Hashes = info.Hashes is not null ? new(info.Hashes) : null;
        MediaInfo = info.MediaInfo is not null ? new(info.MediaInfo) : null;
        CrossReferences = info.CrossReferences.Select(xref => new ReleaseVideoCrossReference(xref)).ToList();
        ReleasedAt = info.ReleasedAt;
        CreatedAt = info.CreatedAt;
    }
}
