
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Extensions;

namespace Shoko.Server.Models.Release;

public class StoredReleaseInfo : IReleaseInfo, IReleaseGroup, IReleaseMediaInfo, IHashes, IEquatable<StoredReleaseInfo>
{
    public StoredReleaseInfo() { }

    public StoredReleaseInfo(IVideo video, IReleaseInfo releaseInfo)
    {
        ED2K = video.Hashes.ED2K;

        ID = releaseInfo.ID;
        ProviderID = releaseInfo.ProviderID;
        ReleaseURI = releaseInfo.ReleaseURI;
        Revision = releaseInfo.Revision;
        Comment = releaseInfo.Comment;
        OriginalFilename = releaseInfo.OriginalFilename;
        IsCensored = releaseInfo.IsCensored;
        IsCorrupted = releaseInfo.IsCorrupted;
        Source = releaseInfo.Source;
        if (releaseInfo.Group is { } group)
        {
            GroupID = group.ID;
            GroupSource = group.Source;
            GroupName = group.Name;
            GroupShortName = group.ShortName;
        }
        if (releaseInfo.Hashes is { } hashes)
        {
            Hashes = $"{hashes.ED2K},{hashes.MD5 ?? string.Empty},{hashes.SHA1 ?? string.Empty},{hashes.CRC ?? string.Empty}";
        }
        if (releaseInfo.MediaInfo is { } mediaInfo)
        {
            AudioLanguages = mediaInfo.AudioLanguages;
            SubtitleLanguages = mediaInfo.SubtitleLanguages;
        }
        CrossReferences = releaseInfo.CrossReferences;
        ReleasedAt = releaseInfo.ReleasedAt;
        LastUpdatedAt = releaseInfo.LastUpdatedAt;
        CreatedAt = releaseInfo.CreatedAt;
    }

    public int StoredReleaseInfoID { get; set; }

    public string ED2K { get; set; } = string.Empty;

    public long FileSize { get; set; }

    #region IReleaseInfo Implementation

    public string? ID { get; set; }

    public string ProviderID { get; set; } = string.Empty;

    public string? ReleaseURI { get; set; }

    public int Revision { get; set; }

    public string? Comment { get; set; }

    public string? OriginalFilename { get; set; }

    public bool? IsCensored { get; set; }

    public bool IsCorrupted { get; set; }

    public bool? IsChaptered { get; set; }

    public ReleaseSource Source { get; set; }

    public string LegacySource => Source switch
    {
        ReleaseSource.TV => "tv",
        ReleaseSource.Web => "www",
        ReleaseSource.DVD => "dvd",
        ReleaseSource.BluRay => "bluray",
        ReleaseSource.VHS => "vhs",
        ReleaseSource.Camera => "camcorder",
        ReleaseSource.VCD => "vcd",
        ReleaseSource.LaserDisc => "ld",
        _ => "unk",
    };

    public string EmbeddedCrossReferences { get; set; } = string.Empty;

    public DateOnly? ReleasedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    long? IReleaseInfo.FileSize => FileSize;

    public IReadOnlyList<IReleaseVideoCrossReference> CrossReferences
    {
        get => EmbeddedCrossReferences
            .Split(',')
            .Select(EmbeddedCrossReference.FromString)
            .WhereNotNull()
            .ToList();
        set => EmbeddedCrossReferences = value
            .Select(x => x.ToEmbeddedString())
            .Join(',');
    }

    #endregion

    #region IReleaseGroup Implementation

    public string? GroupID { get; set; }

    public string? GroupSource { get; set; }

    public string? GroupName { get; set; }

    public string? GroupShortName { get; set; }

    public bool Equals(IReleaseGroup? other)
        => other is not null &&
            string.Equals(GroupID, other.ID) &&
            string.Equals(GroupSource, other.Source);

    IReleaseGroup? IReleaseInfo.Group => !string.IsNullOrEmpty(GroupID) && !string.IsNullOrEmpty(GroupSource) && !string.IsNullOrEmpty(GroupName) && !string.IsNullOrEmpty(GroupShortName) ? this : null;

    string IReleaseGroup.ID => GroupID ?? string.Empty;

    string IReleaseGroup.Source => GroupSource ?? string.Empty;

    string IReleaseGroup.Name => GroupName ?? string.Empty;

    string IReleaseGroup.ShortName => GroupShortName ?? string.Empty;

    #endregion

    #region IReleaseMediaInfo Implementation

    public string? EmbeddedAudioLanguages { get; set; }

    public string? EmbeddedSubtitleLanguages { get; set; }

    public IReadOnlyList<TitleLanguage>? AudioLanguages
    {
        get => EmbeddedAudioLanguages?.Split(',').Select(l => l.Trim().GetTitleLanguage()).ToList();
        set => EmbeddedAudioLanguages = value?.Select(l => l.GetString()).Join(",");
    }

    public IReadOnlyList<TitleLanguage>? SubtitleLanguages
    {
        get => EmbeddedSubtitleLanguages?.Split(',').Select(l => l.Trim().GetTitleLanguage()).ToList();
        set => EmbeddedSubtitleLanguages = value?.Select(l => l.GetString()).Join(",");
    }

    IReleaseMediaInfo? IReleaseInfo.MediaInfo => AudioLanguages is not null || SubtitleLanguages is not null ? this : null;

    IReadOnlyList<TitleLanguage> IReleaseMediaInfo.AudioLanguages => AudioLanguages ?? [];

    IReadOnlyList<TitleLanguage> IReleaseMediaInfo.SubtitleLanguages => SubtitleLanguages ?? [];

    #endregion

    #region IHashes Implementation

    public string? Hashes { get; set; }

    string IHashes.ED2K => this[HashAlgorithmName.ED2K] ?? string.Empty;

    string? IHashes.MD5 => this[HashAlgorithmName.MD5];

    string? IHashes.SHA1 => this[HashAlgorithmName.SHA1];

    string? IHashes.CRC => this[HashAlgorithmName.CRC32];

    public string? this[HashAlgorithmName algorithm]
    {
        get
        {
            var (ed2k, md5, sha1, crc32) = Hashes?.Split('|', StringSplitOptions.TrimEntries) ?? [];
            return algorithm switch
            {
                HashAlgorithmName.ED2K => string.IsNullOrEmpty(ed2k) ? null : ed2k,
                HashAlgorithmName.MD5 => string.IsNullOrEmpty(md5) ? null : md5,
                HashAlgorithmName.SHA1 => string.IsNullOrEmpty(sha1) ? null : sha1,
                HashAlgorithmName.CRC32 => string.IsNullOrEmpty(crc32) ? null : crc32,
                _ => null,
            };
        }
        set
        {
            var (ed2k, md5, sha1, crc32) = Hashes?.Split('|', StringSplitOptions.TrimEntries) ?? [];
            ed2k ??= string.Empty;
            md5 ??= string.Empty;
            sha1 ??= string.Empty;
            crc32 ??= string.Empty;
            switch (algorithm)
            {
                case HashAlgorithmName.ED2K:
                    ed2k = value;
                    break;

                case HashAlgorithmName.MD5:
                    md5 = value;
                    break;

                case HashAlgorithmName.SHA1:
                    sha1 = value;
                    break;

                case HashAlgorithmName.CRC32:
                    crc32 = value;
                    break;
            }

            Hashes = string.IsNullOrEmpty(ed2k) && string.IsNullOrEmpty(md5) && string.IsNullOrEmpty(sha1) && string.IsNullOrEmpty(crc32)
                ? null : $"{ed2k}|{md5}|{sha1}|{crc32}";
        }
    }

    IHashes? IReleaseInfo.Hashes => string.IsNullOrEmpty(Hashes) ? null : this;

    public static bool operator ==(StoredReleaseInfo? left, StoredReleaseInfo? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(StoredReleaseInfo? left, StoredReleaseInfo? right)
        => !(left == right);

    public bool Equals(StoredReleaseInfo? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return GetHashCode() != other.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as StoredReleaseInfo);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            HashCode.Combine(
                ED2K,
                ID,
                ProviderID,
                ReleaseURI,
                Revision,
                Comment,
                OriginalFilename,
                ReleasedAt
            ),
            HashCode.Combine(
                GroupID,
                GroupSource,
                GroupName,
                GroupShortName
            ),
            HashCode.Combine(
                IsCensored,
                IsCorrupted,
                IsChaptered,
                Source,
                Hashes,
                AudioLanguages,
                SubtitleLanguages,
                FileSize
            )
        );
    }

    #endregion
}
