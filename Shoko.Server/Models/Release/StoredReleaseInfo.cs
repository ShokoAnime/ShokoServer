using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models.Release;

public class StoredReleaseInfo : IReleaseInfo, IReleaseGroup, IReleaseMediaInfo, IEquatable<StoredReleaseInfo>
{
    public StoredReleaseInfo() { }

    public StoredReleaseInfo(IVideo video, IReleaseInfo releaseInfo)
    {
        ED2K = video.ED2K;
        FileSize = video.Size;

        ID = releaseInfo.ID;
        ProviderName = releaseInfo.ProviderName;
        ReleaseURI = releaseInfo.ReleaseURI;
        Version = releaseInfo.Version;
        ProvidedFileSize = releaseInfo.FileSize;
        Comment = releaseInfo.Comment;
        OriginalFilename = releaseInfo.OriginalFilename;
        IsCensored = releaseInfo.IsCensored;
        IsCreditless = releaseInfo.IsCreditless;
        IsChaptered = releaseInfo.IsChaptered;
        IsCorrupted = releaseInfo.IsCorrupted;
        IsDeprecated = releaseInfo.IsDeprecated;
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
            Hashes = hashes.Select(x => new HashDigest { Type = x.Type, Value = x.Value, Metadata = x.Metadata }).ToList();
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
        IsPublic = releaseInfo.IsPublic;
        PreventRescan = releaseInfo.PreventRescan;
    }

    public int StoredReleaseInfoID { get; set; }

    public string ED2K { get; set; } = string.Empty;

    /// <summary>
    /// Is used together with <see cref="ED2K"/> to identify which video this
    /// release is tied to.
    /// </summary>
    public long FileSize { get; set; }

    #region IReleaseInfo Implementation

    public string? ID { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string? ReleaseURI { get; set; }

    public int Version { get; set; }

    /// <summary>
    /// The file size for the found release provided by the release provider or user, if any.
    /// </summary>
    public long? ProvidedFileSize { get; set; }

    public string? Comment { get; set; }

    public string? OriginalFilename { get; set; }

    public bool? IsCensored { get; set; }

    public bool? IsCreditless { get; set; }

    public bool? IsChaptered { get; set; }

    public bool IsCorrupted { get; set; }

    public bool IsDeprecated { get; set; }

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
        ReleaseSource.Film => "film",
        _ => "unk",
    };

    /// <summary>
    ///   The hashes as stored, a JSON list. Held as text until
    ///   <see cref="Hashes"/> is first read, then written from the list, or
    ///   kept as text when the list would write it differently.
    /// </summary>
    /// <remarks>
    ///   A JSON list so each hash can be queried once NHibernate and the
    ///   in-memory cache are gone.
    /// </remarks>
    public string? EmbeddedHashes
    {
        get => _hashes is { } read ? read.Text ?? JsonConvert.SerializeObject(read.List) : _hashesText;
        set
        {
            _hashesText = value;
            _hashes = null;
        }
    }

    /// <summary>
    ///   The cross-references as stored, a JSON list. Read into
    ///   <see cref="CrossReferences"/> when set and written from it, or kept as
    ///   text when the list would write it differently.
    /// </summary>
    /// <remarks>
    ///   A JSON list so each cross-reference can be queried once NHibernate
    ///   and the in-memory cache are gone.
    /// </remarks>
    public string EmbeddedCrossReferences
    {
        get => _crossReferencesText ?? JsonConvert.SerializeObject(_crossReferences);
        set
        {
            var crossReferences = JsonConvert.DeserializeObject<List<EmbeddedCrossReference>>(value) ?? [];
            _crossReferences = crossReferences;
            _crossReferencesText = JsonConvert.SerializeObject(crossReferences) == value ? null : value;
        }
    }

    public string? Metadata { get; set; }

    public DateOnly? ReleasedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool? IsPublic { get; set; }

    public bool PreventRescan { get; set; }

    /// <summary>
    /// When <c>true</c>, a subsequent provider in the chain may replace this
    /// result. Set from <see cref="IReleaseInfo.DeferToNext"/>. Cleared once
    /// a definitive result is saved or the chain finalizes.
    /// </summary>
    public bool DeferToNext { get; set; }

    private IReadOnlyList<EmbeddedCrossReference> _crossReferences = [];

    private string? _crossReferencesText;

    public IReadOnlyList<IReleaseVideoCrossReference> CrossReferences
    {
        get => _crossReferences;
        set
        {
            _crossReferences = value.Select(x => new EmbeddedCrossReference(x)).ToList();
            _crossReferencesText = null;
        }
    }

    private string? _hashesText;

    private ReadHashes? _hashes;

    public List<HashDigest>? Hashes
    {
        get
        {
            if (_hashes is { } read)
                return read.List;

            var text = _hashesText;
            if (string.IsNullOrEmpty(text))
                return null;

            var hashes = JsonConvert.DeserializeObject<List<HashDigest>>(text);
            foreach (var hash in hashes ?? [])
            {
                hash.Type = StringPool.Get(hash.Type);
                if (string.Equals(hash.Value, ED2K))
                    hash.Value = ED2K;
            }

            // The first reader's list is the one kept, so every caller sees the same list.
            var parsed = new ReadHashes(hashes, JsonConvert.SerializeObject(hashes) == text ? null : text);
            var current = Interlocked.CompareExchange(ref _hashes, parsed, null) ?? parsed;
            if (ReferenceEquals(current, parsed))
                _hashesText = null;

            return current.List;
        }
        set
        {
            _hashesText = null;
            _hashes = value is null ? null : new(value, null);
        }
    }

    /// <summary>
    ///   The hashes read from <see cref="EmbeddedHashes"/>.
    /// </summary>
    /// <param name="List">The hashes.</param>
    /// <param name="Text">The stored text, when the list would write it differently.</param>
    private sealed record ReadHashes(List<HashDigest>? List, string? Text);

    long? IReleaseInfo.FileSize => ProvidedFileSize;

    IReadOnlyList<IHashDigest>? IReleaseInfo.Hashes => Hashes;

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


    public static bool operator ==(StoredReleaseInfo? left, StoredReleaseInfo? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(StoredReleaseInfo? left, StoredReleaseInfo? right)
        => !(left == right);

    /// <summary>
    ///   Whether two releases are the same release: every field a provider or
    ///   user gives it, flags included, with the lists compared as stored.
    ///   The row's ID, its times and <see cref="DeferToNext"/> do not count.
    /// </summary>
    /// <param name="other">The other release.</param>
    /// <returns><c>true</c> when they are the same release.</returns>
    public bool Equals(StoredReleaseInfo? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return string.Equals(ED2K, other.ED2K, StringComparison.Ordinal) &&
            FileSize == other.FileSize &&
            string.Equals(ID, other.ID, StringComparison.Ordinal) &&
            string.Equals(ProviderName, other.ProviderName, StringComparison.Ordinal) &&
            string.Equals(ReleaseURI, other.ReleaseURI, StringComparison.Ordinal) &&
            Version == other.Version &&
            ProvidedFileSize == other.ProvidedFileSize &&
            string.Equals(Comment, other.Comment, StringComparison.Ordinal) &&
            string.Equals(OriginalFilename, other.OriginalFilename, StringComparison.Ordinal) &&
            string.Equals(Metadata, other.Metadata, StringComparison.Ordinal) &&
            ReleasedAt == other.ReleasedAt &&
            Source == other.Source &&
            IsCensored == other.IsCensored &&
            IsCreditless == other.IsCreditless &&
            IsChaptered == other.IsChaptered &&
            IsCorrupted == other.IsCorrupted &&
            IsDeprecated == other.IsDeprecated &&
            IsPublic == other.IsPublic &&
            PreventRescan == other.PreventRescan &&
            string.Equals(GroupID, other.GroupID, StringComparison.Ordinal) &&
            string.Equals(GroupSource, other.GroupSource, StringComparison.Ordinal) &&
            string.Equals(GroupName, other.GroupName, StringComparison.Ordinal) &&
            string.Equals(GroupShortName, other.GroupShortName, StringComparison.Ordinal) &&
            string.Equals(EmbeddedCrossReferences, other.EmbeddedCrossReferences, StringComparison.Ordinal) &&
            string.Equals(EmbeddedHashes, other.EmbeddedHashes, StringComparison.Ordinal) &&
            string.Equals(EmbeddedAudioLanguages, other.EmbeddedAudioLanguages, StringComparison.Ordinal) &&
            string.Equals(EmbeddedSubtitleLanguages, other.EmbeddedSubtitleLanguages, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as StoredReleaseInfo);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ED2K, StringComparer.Ordinal);
        hash.Add(FileSize);
        hash.Add(ID, StringComparer.Ordinal);
        hash.Add(ProviderName, StringComparer.Ordinal);
        hash.Add(ReleaseURI, StringComparer.Ordinal);
        hash.Add(Version);
        hash.Add(ProvidedFileSize);
        hash.Add(Comment, StringComparer.Ordinal);
        hash.Add(OriginalFilename, StringComparer.Ordinal);
        hash.Add(Metadata, StringComparer.Ordinal);
        hash.Add(ReleasedAt);
        hash.Add(Source);
        hash.Add(IsCensored);
        hash.Add(IsCreditless);
        hash.Add(IsChaptered);
        hash.Add(IsCorrupted);
        hash.Add(IsDeprecated);
        hash.Add(IsPublic);
        hash.Add(PreventRescan);
        hash.Add(GroupID, StringComparer.Ordinal);
        hash.Add(GroupSource, StringComparer.Ordinal);
        hash.Add(GroupName, StringComparer.Ordinal);
        hash.Add(GroupShortName, StringComparer.Ordinal);
        hash.Add(EmbeddedCrossReferences, StringComparer.Ordinal);
        hash.Add(EmbeddedHashes, StringComparer.Ordinal);
        hash.Add(EmbeddedAudioLanguages, StringComparer.Ordinal);
        hash.Add(EmbeddedSubtitleLanguages, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns true if the provider name matches the given provider name.
    /// </summary>
    /// <param name="providerName">
    ///   The provider name.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the provider name matches the given provider name;
    ///   otherwise, <c>false</c>.
    /// </returns>
    public bool HasProviderName(string providerName)
        => ProviderName == providerName ||
           ProviderName.StartsWith(providerName + "+") ||
           ProviderName.EndsWith("+" + providerName) ||
           ProviderName.Contains("+" + providerName + "+");

    #endregion
}
