
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class ReleaseInfoSignalRModel : IReleaseInfo
{
    /// <inheritdoc />
    public string? ID { get; init; }

    /// <inheritdoc />
    public string ProviderName { get; init; }

    /// <inheritdoc />
    public string? ReleaseURI { get; init; }

    /// <inheritdoc />
    public int Revision { get; init; }

    /// <inheritdoc />
    public long? FileSize { get; init; }

    /// <inheritdoc />
    public string? Comment { get; init; }

    /// <inheritdoc />
    public string? OriginalFilename { get; init; }

    /// <inheritdoc />
    public bool? IsCensored { get; init; }

    /// <inheritdoc />
    public bool? IsChaptered { get; init; }

    /// <inheritdoc />
    public bool? IsCreditless { get; init; }

    /// <inheritdoc />
    public bool IsCorrupted { get; init; }

    /// <inheritdoc />
    [JsonConverter(typeof(StringEnumConverter))]
    public ReleaseSource Source { get; init; }

    /// <inheritdoc />
    public ReleaseGroup? Group { get; init; }

    /// <inheritdoc />
    public List<HashDigest>? Hashes { get; init; }

    /// <inheritdoc />
    public ReleaseMediaInfo? MediaInfo { get; init; }

    /// <inheritdoc />
    [Required]
    [MinLength(1)]
    public IReadOnlyList<ReleaseVideoCrossReference> CrossReferences { get; init; }

    /// <inheritdoc />
    public string? Metadata { get; init; }

    /// <inheritdoc />
    public DateOnly? Released { get; init; }

    /// <inheritdoc />
    public DateTime Updated { get; init; }

    /// <inheritdoc />
    public DateTime Created { get; init; }

    public ReleaseInfoSignalRModel(IReleaseInfo releaseInfo)
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
        Hashes = releaseInfo.Hashes?.Select(h => new HashDigest() { Type = h.Type, Value = h.Value, Metadata = h.Metadata }).ToList();
        MediaInfo = releaseInfo.MediaInfo is not null ? new(releaseInfo.MediaInfo) : null;
        CrossReferences = releaseInfo.CrossReferences.Select(x => new ReleaseVideoCrossReference(x)).ToList();
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
