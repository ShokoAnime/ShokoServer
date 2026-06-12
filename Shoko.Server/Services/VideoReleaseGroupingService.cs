using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.MediaInfo;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Services;

/// <summary>
/// A <see cref="VideoLocal_Place"/> with its associated video and optional
/// release info already loaded, ready to pass to
/// <see cref="VideoReleaseGroupingService.Group(IEnumerable{ResolvedVideoPlace})"/>.
/// </summary>
public record ResolvedVideoPlace(VideoLocal_Place Place, VideoLocal Video, StoredReleaseInfo? ReleaseInfo);

/// <summary>
/// Groups <see cref="VideoLocal_Place"/> objects into <see cref="VideoReleaseCandidate"/>
/// buckets using a deterministic heuristic over release-provider metadata and
/// MediaInfo stream signals.
/// </summary>
/// <remarks>
/// <para>
/// A release contains exactly one file per episode. Two files that cover the
/// same episode must therefore belong to different releases — unless one is
/// a version-patch of the other within the same release (e.g. v2 correcting
/// a single episode while the rest remain v1).
/// </para>
/// <para>
/// Grouping is a two-pass process:
/// </para>
/// <list type="number">
///   <item>
///     <b>Fuzzy grouping</b> — files are placed into the first bucket where
///     no known signal conflicts with the bucket's existing files. A missing
///     field (null, empty, or unknown) is treated as a wildcard and never
///     causes a split on its own. Only when <em>both</em> sides of a comparison
///     have a known, non-default value that differs is a conflict declared.
///     SRI-backed files are processed first so they seed buckets with complete
///     signals before unrecognised files are considered.
///   </item>
///   <item>
///     <b>Episode-collision split</b> — within each bucket, episode identifiers
///     are compared across files. If <em>every</em> episode is covered by more
///     than one file and those files carry different version numbers, the bucket
///     is split by version into separate candidates. When only <em>some</em>
///     episodes collide (mixed-patch release), the bucket stays together.
///   </item>
/// </list>
/// <para>
/// The resulting <see cref="VideoReleaseCandidate.Key"/> is a SHA-256 hex
/// fingerprint of the quality signals (group, source, languages, codec,
/// resolution, stream counts, flags, version). Folder location is excluded so
/// candidates with identical quality profiles share a key.
/// </para>
/// </remarks>
public class VideoReleaseGroupingService(
    VideoLocalRepository videoLocalRepository,
    StoredReleaseInfoRepository releaseInfoRepository)
{
    /// <summary>
    /// Groups the given places into release candidates, resolving the
    /// associated video and release info from the repositories.
    /// </summary>
    public IReadOnlyList<VideoReleaseCandidate> Group(IEnumerable<VideoLocal_Place> places)
        => Group(places.Select(p =>
        {
            var video = videoLocalRepository.GetByID(p.VideoID);
            if (video is null)
                return null;
            var sri = releaseInfoRepository.GetByEd2kAndFileSize(video.Hash, video.FileSize);
            return new ResolvedVideoPlace(p, video, sri);
        }).OfType<ResolvedVideoPlace>());

    /// <summary>
    /// Groups pre-resolved place records into release candidates.
    /// Useful when the caller already has the video and release info loaded,
    /// or for testing without repository dependencies.
    /// </summary>
    public IReadOnlyList<VideoReleaseCandidate> Group(IEnumerable<ResolvedVideoPlace> resolved)
        => FuzzyGroup(resolved.Select(BuildSignature).ToList())
            .SelectMany(SplitOnEpisodeCollisions)
            .Select(BuildCandidate)
            .ToList();

    private static FileSignature BuildSignature(ResolvedVideoPlace r)
    {
        var (place, video, sri) = r;
        var media = video.MediaInfo;

        var videoStream = media?.VideoStream;
        var audioStream = media?.AudioStreams.FirstOrDefault();
        var generalStream = media?.IsUsable == true ? media.GeneralStream : null;

        var videoCodec = videoStream is not null
            ? MediaInfoUtility.TranslateCodec(videoStream) ?? videoStream.Format?.ToUpperInvariant()
            : null;
        var audioCodec = audioStream is not null
            ? MediaInfoUtility.TranslateCodec(audioStream) ?? audioStream.Format?.ToUpperInvariant()
            : null;
        var resolution = videoStream is not null && (videoStream.Width > 0 || videoStream.Height > 0)
            ? MediaInfoUtility.GetStandardResolution(Tuple.Create(videoStream.Width, videoStream.Height))
              ?? $"{videoStream.Width}x{videoStream.Height}"
            : null;
        var bitDepth = videoStream?.BitDepth ?? 0;
        var container = generalStream?.Format?.ToUpperInvariant();
        var audioStreamCount = media?.AudioStreams.Count ?? 0;
        var subtitleStreamCount = media?.TextStreams.Count ?? 0;

        IReadOnlyList<TitleLanguage> audioLangs;
        IReadOnlyList<TitleLanguage> subLangs;
        if (sri is not null)
        {
            // Filter TitleLanguage.None — an empty/unset entry in SRI means "unknown", not a language.
            audioLangs = sri.AudioLanguages?.Where(l => l != TitleLanguage.None).ToList() ?? [];
            subLangs = sri.SubtitleLanguages?.Where(l => l != TitleLanguage.None).ToList() ?? [];
        }
        else
        {
            audioLangs = media?.AudioStreams
                .Select(a => a.Language?.GetTitleLanguage() ?? TitleLanguage.None)
                .Where(l => l != TitleLanguage.None)
                .Distinct()
                .ToList() ?? [];
            subLangs = media?.TextStreams
                .Where(t => !t.External)
                .Select(t => t.Language?.GetTitleLanguage() ?? TitleLanguage.None)
                .Where(l => l != TitleLanguage.None)
                .Distinct()
                .ToList() ?? [];
        }

        bool? isChaptered = sri?.IsChaptered;
        if (isChaptered is null && (media as Shoko.Abstractions.Video.Media.IMediaInfo)?.Chapters.Count > 0)
            isChaptered = true;

        var episodeIds = sri?.CrossReferences
            .Select(x => x is EmbeddedCrossReference ecr
                ? (ecr.EpisodeType, ecr.EpisodeNumber)
                : (EpisodeType.Episode, x.AnidbEpisodeID))
            .Where(k => k.Item2 > 0)
            .ToHashSet() ?? new HashSet<(EpisodeType, int)>();

        return new FileSignature
        {
            Place = place,
            ReleaseInfo = sri,
            AudioLanguages = audioLangs,
            SubtitleLanguages = subLangs,
            VideoCodec = videoCodec,
            BitDepth = bitDepth,
            Resolution = resolution,
            AudioCodec = audioCodec,
            Container = container,
            AudioStreamCount = audioStreamCount,
            SubtitleStreamCount = subtitleStreamCount,
            IsChaptered = isChaptered,
            EpisodeIds = episodeIds,
        };
    }

    /// <summary>
    /// Places each signature into the first existing bucket where it does not
    /// conflict with any file already in that bucket. Files with SRI are
    /// processed first so they seed buckets with complete signals.
    /// A missing field on either side is never a conflict.
    /// </summary>
    private static List<List<FileSignature>> FuzzyGroup(List<FileSignature> signatures)
    {
        // SRI-backed files first — they carry more complete signals and seed buckets
        var ordered = signatures.OrderByDescending(s => s.ReleaseInfo is not null).ToList();
        var buckets = new List<List<FileSignature>>();

        foreach (var sig in ordered)
        {
            var target = buckets.FirstOrDefault(b => b.All(existing => AreCompatible(sig, existing)));
            if (target is not null)
                target.Add(sig);
            else
                buckets.Add([sig]);
        }

        return buckets;
    }

    /// <summary>
    /// Returns true when the two signatures do not conflict on any known signal.
    /// Missing values (null, empty, zero, Unknown enum, empty language list) on
    /// either side are treated as wildcards.
    /// </summary>
    private static bool AreCompatible(FileSignature a, FileSignature b)
    {
        // Hard separators — always split regardless of missing data
        if (a.Place.ManagedFolderID != b.Place.ManagedFolderID) return false;
        if (GetParentDirectory(a.Place.RelativePath) != GetParentDirectory(b.Place.RelativePath)) return false;

        // Video encoding signals — only conflict when both are non-null/non-zero and differ
        if (!NullOrCompatible(a.VideoCodec, b.VideoCodec)) return false;
        if (a.BitDepth > 0 && b.BitDepth > 0 && a.BitDepth != b.BitDepth) return false;
        if (!NullOrCompatible(a.Resolution, b.Resolution)) return false;

        // Release group — only conflicts when both carry a known group that differs
        if (!NullOrCompatible(GroupKey(a.ReleaseInfo), GroupKey(b.ReleaseInfo))) return false;

        // Source — only conflicts when both are explicitly set and differ
        var aSource = a.ReleaseInfo?.Source ?? ReleaseSource.Unknown;
        var bSource = b.ReleaseInfo?.Source ?? ReleaseSource.Unknown;
        if (aSource != ReleaseSource.Unknown && bSource != ReleaseSource.Unknown && aSource != bSource) return false;

        // Language sets — only conflict when both sides have at least one identified language and the sets differ
        if (a.AudioLanguages.Count > 0 && b.AudioLanguages.Count > 0 && !LanguageSetsMatch(a.AudioLanguages, b.AudioLanguages)) return false;
        if (a.SubtitleLanguages.Count > 0 && b.SubtitleLanguages.Count > 0 && !LanguageSetsMatch(a.SubtitleLanguages, b.SubtitleLanguages)) return false;

        // Audio codec and container — soft separators
        if (!NullOrCompatible(a.AudioCodec, b.AudioCodec)) return false;
        if (!NullOrCompatible(a.Container, b.Container)) return false;

        return true;
    }

    private static string? GroupKey(StoredReleaseInfo? sri) =>
        !string.IsNullOrEmpty(sri?.GroupID) && !string.IsNullOrEmpty(sri?.GroupSource)
            ? $"{sri.GroupID}|{sri.GroupSource}"
            : null;

    private static bool NullOrCompatible(string? a, string? b) =>
        string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b;

    private static bool LanguageSetsMatch(IReadOnlyList<TitleLanguage> a, IReadOnlyList<TitleLanguage> b)
    {
        if (a.Count != b.Count) return false;
        var setA = a.ToHashSet();
        return b.All(l => setA.Contains(l));
    }

    /// <summary>
    /// Checks whether a bucket needs to be split into per-version candidates.
    /// A split occurs only when <em>every</em> episode in the bucket is covered
    /// by files from more than one version — indicating parallel complete
    /// releases rather than a mixed-patch single release.
    /// </summary>
    private static IEnumerable<List<FileSignature>> SplitOnEpisodeCollisions(List<FileSignature> signatures)
    {
        // Without episode data on every file we cannot check coverage
        if (signatures.Any(s => s.ReleaseInfo is null || s.EpisodeIds.Count == 0))
        {
            yield return signatures;
            yield break;
        }

        // Map each episode to the set of files that cover it
        var episodeToFiles = signatures
            .SelectMany(s => s.EpisodeIds.Select(id => (EpisodeId: id, Sig: s)))
            .GroupBy(x => x.EpisodeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Sig).ToList());

        var colliding = episodeToFiles.Where(kv => kv.Value.Count > 1).ToList();

        // No collisions → single release
        if (colliding.Count == 0)
        {
            yield return signatures;
            yield break;
        }

        // Some-but-not-all episodes collide → mixed-patch single release; keep together.
        // This is the Air scenario: most episodes are v1, a handful were later patched to v2.
        if (colliding.Count < episodeToFiles.Count)
        {
            yield return signatures;
            yield break;
        }

        // Every episode has multiple files — check version differences first
        var versionsInFirstCollision = colliding[0].Value
            .Select(s => s.ReleaseInfo!.Version)
            .Distinct()
            .ToList();
        if (versionsInFirstCollision.Count > 1)
        {
            // True parallel releases — split one bucket per version
            foreach (var versionGroup in signatures.GroupBy(s => s.ReleaseInfo!.Version))
                yield return [.. versionGroup];
            yield break;
        }

        // Same version — check if colliding files differ on quality (corrupt vs clean, chaptered vs not).
        // Within a single release, quality flags can vary per-file; only split when there IS an
        // alternative (all episodes collide) and the alternatives genuinely differ in quality.
        var qualityTierGroups = signatures.GroupBy(QualityTierKey).ToList();
        if (qualityTierGroups.Count > 1)
        {
            foreach (var tierGroup in qualityTierGroups)
                yield return [.. tierGroup];
            yield break;
        }

        // Same version, same quality tier — keep together (ambiguous, e.g. combined-episode file)
        yield return signatures;
    }

    /// <summary>
    /// Groups the colliding files by their per-file quality tier so that
    /// "better" files (not corrupt, chaptered) form one candidate while
    /// "worse" files form another, preserving the ability to prefer one over the other.
    /// </summary>
    private static string QualityTierKey(FileSignature sig)
    {
        var isCorrupted = sig.ReleaseInfo?.IsCorrupted ?? false;
        var isChaptered = sig.IsChaptered ?? false;
        // Higher "true" values indicate better quality; distinct combinations form separate tiers
        return $"{!isCorrupted}|{isChaptered}";
    }

    private static VideoReleaseCandidate BuildCandidate(List<FileSignature> signatures)
    {
        // Use the first SRI-backed file as the representative for group metadata only.
        var rep = signatures.FirstOrDefault(s => s.ReleaseInfo is not null) ?? signatures[0];
        var sri = rep.ReleaseInfo;

        // Quality signals: most common non-null value across all files (majority vote).
        // Prevents one outlier file (e.g. a bonus H264 OVA in an otherwise HEVC release)
        // from misrepresenting the release's actual quality tier.
        var videoCodec          = MajorityRef(signatures.Select(s => s.VideoCodec));
        var resolution          = MajorityRef(signatures.Select(s => s.Resolution));
        var bitDepth            = Majority(signatures.Select(s => s.BitDepth > 0 ? (int?)s.BitDepth : null)) ?? 0;
        var audioCodec          = MajorityRef(signatures.Select(s => s.AudioCodec));
        var container           = MajorityRef(signatures.Select(s => s.Container));
        var audioStreamCount    = Majority(signatures.Select(s => s.AudioStreamCount > 0 ? (int?)s.AudioStreamCount : null)) ?? 0;
        var subtitleStreamCount = Majority(signatures.Select(s => s.SubtitleStreamCount > 0 ? (int?)s.SubtitleStreamCount : null)) ?? 0;
        var source              = Majority(signatures.Select(s => s.ReleaseInfo?.Source is { } src and not ReleaseSource.Unknown ? (ReleaseSource?)src : null))
                                  ?? ReleaseSource.Unknown;
        var version             = Majority(signatures.Select(s => s.ReleaseInfo?.Version is > 0 ? (int?)s.ReleaseInfo.Version : null)) ?? 0;
        var audioLanguages      = MajorityLanguageSet(signatures.Select(s => s.AudioLanguages));
        var subtitleLanguages   = MajorityLanguageSet(signatures.Select(s => s.SubtitleLanguages));

        // "Any true" semantics: these flags warn about or advertise the whole candidate.
        var isCorrupted = signatures.Any(s => s.ReleaseInfo?.IsCorrupted == true);
        var isChaptered = signatures.Any(s => s.IsChaptered == true) ? (bool?)true
            : signatures.Any(s => s.IsChaptered == false) ? false
            : null;
        var isCensored  = signatures.Any(s => s.ReleaseInfo?.IsCensored == true) ? (bool?)true
            : signatures.Any(s => s.ReleaseInfo?.IsCensored == false) ? false
            : null;

        // Episode coverage: union of all files' episode IDs.
        var episodeCoverage = signatures.SelectMany(s => s.EpisodeIds).ToHashSet();

        var sortedAudio = string.Join(",", audioLanguages.Select(l => l.GetString()).Order());
        var sortedSubs  = string.Join(",", subtitleLanguages.Select(l => l.GetString()).Order());

        var key = ComputeKey(
            sri?.GroupID, sri?.GroupSource,
            source,
            sortedAudio, sortedSubs,
            videoCodec, bitDepth, resolution,
            audioCodec, container,
            audioStreamCount, subtitleStreamCount,
            isChaptered, isCensored, sri?.IsCreditless,
            isCorrupted, version
        );

        return new VideoReleaseCandidate
        {
            Key = key,
            HasReleaseInfo = signatures.All(s => s.ReleaseInfo is not null),
            GroupID = sri?.GroupID,
            GroupSource = sri?.GroupSource,
            GroupName = sri?.GroupName,
            GroupShortName = sri?.GroupShortName,
            Source = source,
            AudioLanguages = audioLanguages,
            SubtitleLanguages = subtitleLanguages,
            VideoCodec = videoCodec,
            BitDepth = bitDepth,
            Resolution = resolution,
            AudioCodec = audioCodec,
            Container = container,
            AudioStreamCount = audioStreamCount,
            SubtitleStreamCount = subtitleStreamCount,
            IsChaptered = isChaptered,
            IsCensored = isCensored,
            IsCreditless = sri?.IsCreditless,
            IsCorrupted = isCorrupted,
            Version = version,
            Places = signatures.Select(s => s.Place).ToList(),
            EpisodeCoverage = episodeCoverage,
        };
    }

    private static T? Majority<T>(IEnumerable<T?> values) where T : struct
        => values.Where(v => v.HasValue)
                 .GroupBy(v => v!.Value)
                 .OrderByDescending(g => g.Count())
                 .Select(g => (T?)g.Key)
                 .FirstOrDefault();

    private static T? MajorityRef<T>(IEnumerable<T?> values) where T : class
        => values.Where(v => v is not null)
                 .GroupBy(v => v)
                 .OrderByDescending(g => g.Count())
                 .Select(g => g.Key)
                 .FirstOrDefault();

    private static IReadOnlyList<TitleLanguage> MajorityLanguageSet(
        IEnumerable<IReadOnlyList<TitleLanguage>> sets)
        => sets.Where(s => s.Count > 0)
               .GroupBy(s => string.Join(",", s.Select(l => l.GetString()).Order()))
               .OrderByDescending(g => g.Count())
               .FirstOrDefault()
               ?.First() ?? [];

    /// <summary>
    /// Returns a SHA-256 hex fingerprint of the given quality signals.
    /// The folder location is intentionally excluded so candidates with
    /// identical quality profiles share a key.
    /// </summary>
    private static string ComputeKey(
        string? groupId, string? groupSource,
        ReleaseSource source,
        string sortedAudio, string sortedSubs,
        string? videoCodec, int bitDepth, string? resolution,
        string? audioCodec, string? container,
        int audioStreamCount, int subtitleStreamCount,
        bool? isChaptered, bool? isCensored, bool? isCreditless,
        bool isCorrupted, int version)
    {
        static string Tri(bool? v) => v is null ? "?" : v.Value ? "1" : "0";

        var signal = string.Join("|",
            groupId ?? string.Empty,
            groupSource ?? string.Empty,
            (int)source,
            sortedAudio,
            sortedSubs,
            videoCodec ?? string.Empty,
            bitDepth,
            resolution ?? string.Empty,
            audioCodec ?? string.Empty,
            container ?? string.Empty,
            audioStreamCount,
            subtitleStreamCount,
            Tri(isChaptered),
            Tri(isCensored),
            Tri(isCreditless),
            isCorrupted ? "1" : "0",
            version
        );
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(signal));
        return Convert.ToHexStringLower(hash);
    }

    private static string GetParentDirectory(string relativePath)
    {
        var lastSlash = relativePath.LastIndexOf('/');
        return lastSlash <= 0 ? string.Empty : relativePath[..lastSlash];
    }

    private sealed class FileSignature
    {
        public required VideoLocal_Place Place { get; init; }
        public StoredReleaseInfo? ReleaseInfo { get; init; }
        public required IReadOnlyList<TitleLanguage> AudioLanguages { get; init; }
        public required IReadOnlyList<TitleLanguage> SubtitleLanguages { get; init; }
        public string? VideoCodec { get; init; }
        public int BitDepth { get; init; }
        public string? Resolution { get; init; }
        public string? AudioCodec { get; init; }
        public string? Container { get; init; }
        public int AudioStreamCount { get; init; }
        public int SubtitleStreamCount { get; init; }
        public bool? IsChaptered { get; init; }
        public IReadOnlySet<(EpisodeType Type, int Number)> EpisodeIds { get; init; } = new HashSet<(EpisodeType, int)>();
    }
}
