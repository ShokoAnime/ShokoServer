
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.API.v3.Models.Shoko;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseInfo : IReleaseInfo
{
    /// <summary>
    /// The ID of the release by the provider, if available from the provider.
    /// </summary>
    public string? ID { get; init; }

    /// <summary>
    /// The name of the provider which found the release. This field can
    /// intentionally be set to anything to allow importing/exporting/remixing
    /// data from other providers.
    /// </summary>
    public string ProviderName { get; init; }

    /// <summary>
    /// An absolute URI for where to find the information, if available from the
    /// provider. Can be a http://, https:// or file:// URI.
    /// </summary>
    public string? ReleaseURI { get; init; }

    /// <summary>
    /// Release revision number. Might be increased each time the
    /// <see cref="Group"/> releases a new version for the same release.
    /// The value is not guaranteed to be unique.
    /// </summary>
    public int Revision { get; init; }

    /// <summary>
    /// File size in bytes, if available from the provider.
    /// </summary>
    public long? FileSize { get; init; }

    /// <summary>
    /// Comment about the release info, if available from the provider.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// The original name of the file, if it's known by the release info
    /// provider.
    /// </summary>
    public string? OriginalFilename { get; init; }

    /// <summary>
    /// Indicates that the release is censored or de-censored. For most releases
    /// this will be <c>null</c>.
    /// </summary>
    public bool? IsCensored { get; init; }

    /// <summary>
    /// Indicates that the release is chaptered, if it's known by the release
    /// info provider.
    /// </summary>
    public bool? IsChaptered { get; init; }

    /// <summary>
    /// Indicates that the release is an OP/ED without credits, if it's known by
    /// the release info provider.
    /// </summary>
    public bool? IsCreditless { get; init; }

    /// <summary>
    /// Indicates that the released file is corrupted.
    /// </summary>
    public bool IsCorrupted { get; init; }

    /// <summary>
    /// The source of the release. What the video file was created from.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public ReleaseSource Source { get; init; }

    /// <summary>
    /// The release group associated with the release, if known. Can be omitted.
    /// </summary>
    public ReleaseGroup? Group { get; init; }

    /// <summary>
    /// Override hashes for the file, if available from the provider.
    /// </summary>
    public List<File.HashDigest>? Hashes { get; init; }

    /// <summary>
    /// Remote media information about the file, if available from the provider.
    /// </summary>
    public ReleaseMediaInfo? MediaInfo { get; init; }

    /// <summary>
    /// All video to episode cross-references included in this release.
    /// </summary>
    [Required]
    [MinLength(1)]
    public IReadOnlyList<ReleaseCrossReference> CrossReferences { get; init; }

    /// <summary>
    /// Metadata about the release from the provider or user, if available.
    /// </summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// When the video was released, according to the provider. May or may not
    /// be accurate, but at least it's something. Can be <c>null</c> if not
    /// known.
    /// </summary>
    public DateOnly? Released { get; init; }

    /// <summary>
    /// When the release information was last updated by the provider or
    /// locally. Up to the provider to decide how to set this, but it should
    /// always be set.
    /// </summary>
    public DateTime Updated { get; init; }

    /// <summary>
    /// When the release information was locally saved in Shoko for the first
    /// time.
    /// </summary>
    public DateTime Created { get; init; }

    public ReleaseInfo()
    {
        ProviderName = "User";
        CrossReferences = [];
    }

    public ReleaseInfo(IReleaseInfo releaseInfo)
    {
        ID = releaseInfo.ID;
        ProviderName = releaseInfo.ProviderName;
        ReleaseURI = releaseInfo.ReleaseURI;
        Revision = releaseInfo.Revision;
        FileSize = releaseInfo.FileSize;
        Comment = releaseInfo.Comment;
        OriginalFilename = releaseInfo.OriginalFilename;
        IsCensored = releaseInfo.IsCensored;
        IsCreditless = releaseInfo.IsCreditless;
        IsChaptered = releaseInfo.IsChaptered;
        IsCorrupted = releaseInfo.IsCorrupted;
        Source = releaseInfo.Source;
        Group = releaseInfo.Group is not null ? new(releaseInfo.Group) : null;
        Hashes = releaseInfo.Hashes?.Select(h => new File.HashDigest(h)).ToList();
        MediaInfo = releaseInfo.MediaInfo is not null ? new(releaseInfo.MediaInfo) : null;
        CrossReferences = releaseInfo.CrossReferences.Select(x => new ReleaseCrossReference(x)).ToList();
        Metadata = releaseInfo.Metadata;
        Released = releaseInfo.ReleasedAt;
        Updated = releaseInfo.LastUpdatedAt;
        Created = releaseInfo.CreatedAt;
    }

    #region IReleaseInfo implementation

    IReleaseGroup? IReleaseInfo.Group => Group;

    IReadOnlyList<IHashDigest>? IReleaseInfo.Hashes => Hashes;

    IReleaseMediaInfo? IReleaseInfo.MediaInfo => MediaInfo;

    IReadOnlyList<IReleaseVideoCrossReference> IReleaseInfo.CrossReferences => CrossReferences;

    DateOnly? IReleaseInfo.ReleasedAt => Released;

    DateTime IReleaseInfo.LastUpdatedAt => Updated;

    DateTime IReleaseInfo.CreatedAt => Created;


    #endregion
}
