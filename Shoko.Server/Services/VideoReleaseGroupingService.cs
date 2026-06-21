#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Media;
using Shoko.Server.MediaInfo;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Services;

/// <summary>
/// A <see cref="VideoLocal_Place"/> with its associated video and optional
/// release info already loaded, ready to pass to
/// <see cref="VideoReleaseGroupingService.Group(System.Collections.Generic.IEnumerable{Shoko.Server.Services.ResolvedVideoPlace})"/>.
/// </summary>
public record ResolvedVideoPlace(VideoLocal_Place Place, VideoLocal Video, StoredReleaseInfo? ReleaseInfo);

/// <summary>
/// Groups <see cref="VideoLocal_Place"/> objects into <see cref="VideoReleaseCandidate"/>
/// buckets using a deterministic heuristic over release-provider metadata and
/// MediaInfo stream signals.
/// </summary>
/// <remarks>
/// Grouping is a four-pass process:
/// <list type="number">
///   <item><b>Fuzzy grouping</b> — files are placed into the first bucket where no
///     known signal conflicts with the bucket's existing files.</item>
///   <item><b>Same-group merge</b> — buckets sharing the same release-group key
///     (GroupID + GroupSource) are merged. Quality variation within a group (e.g.
///     chaptered episodes alongside unchaptered) is reported as a mixed signal on
///     the combined candidate rather than producing separate candidates.</item>
///   <item><b>Version-strategy candidates</b> — within each merged group, episodes covered
///     by multiple version numbers produce a BestAvailable candidate (highest version
///     per episode) and a Consistent(v) candidate for each older version.</item>
///   <item><b>Gap-fill candidates</b> — for each partial-coverage version-strategy spec,
///     one gap-fill is generated per distinct filler group that can fully cover the gap.
///     The filler always uses BestAvailable files for the missing episodes only.</item>
/// </list>
/// </remarks>
public class VideoReleaseGroupingService(
    VideoLocalRepository videoLocalRepository,
    StoredReleaseInfoRepository releaseInfoRepository)
{
    /// <summary>
    /// Fast heuristic that checks whether a series is likely to produce more than
    /// one release candidate. Only inspects cached SRI group keys — no MediaInfo
    /// access, no CrossReference deserialization.
    /// <para>
    /// Returns <c>false</c> only when every file has an identified SRI with the
    /// same release-group key, meaning a single-candidate result is certain.
    /// Returns <c>true</c> when in doubt, so false negatives are avoided.
    /// </para>
    /// </summary>
    public bool MightHaveMultipleCandidates(IEnumerable<VideoLocal> videos)
    {
        var groupKey = (string?)null;
        var episodeVersions = new Dictionary<int, int>();

        foreach (var video in videos)
        {
            var sri = releaseInfoRepository.GetByEd2kAndFileSize(video.Hash, video.FileSize);
            if (sri is null) return true;       // unidentified file
            var key = GroupKey(sri);
            if (key is null) return true;       // SRI has no group info
            if (groupKey is null) groupKey = key;
            else if (groupKey != key) return true;

            // Version collision within the same group → GenerateVersionStrategyCandidates
            // would produce BestAvailable + Consistent variants.
            foreach (var xref in sri.CrossReferences)
            {
                if (episodeVersions.TryGetValue(xref.AnidbEpisodeID, out var existing))
                {
                    if (existing != sri.Version) return true;
                }
                else
                {
                    episodeVersions[xref.AnidbEpisodeID] = sri.Version;
                }
            }
        }
        return false;
    }

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
    /// Returns one <see cref="VideoReleaseOverride"/> per merged release-group bucket —
    /// the same FuzzyGroup + same-group-merge passes as <see cref="Group(IEnumerable{VideoLocal_Place})"/>
    /// but without the coverage filter or version-strategy split. Used as the data
    /// source for the release override (Mix &amp; Match) view.
    /// </summary>
    public IReadOnlyList<VideoReleaseOverride> GetOverrides(IEnumerable<VideoLocal_Place> places)
        => GetOverrides(places.Select(p =>
        {
            var video = videoLocalRepository.GetByID(p.VideoID);
            if (video is null)
                return null;
            var sri = releaseInfoRepository.GetByEd2kAndFileSize(video.Hash, video.FileSize);
            return new ResolvedVideoPlace(p, video, sri);
        }).OfType<ResolvedVideoPlace>());

    /// <summary>
    /// Returns one <see cref="VideoReleaseOverride"/> per merged release-group bucket
    /// from pre-resolved place records.
    /// </summary>
    public IReadOnlyList<VideoReleaseOverride> GetOverrides(IEnumerable<ResolvedVideoPlace> resolved)
    {
        var sigs = resolved.Select(BuildSignature).ToList();
        var allEpisodes = (IReadOnlySet<(EpisodeType, int)>)sigs.SelectMany(s => s.EpisodeIds).ToHashSet();
        var buckets = FuzzyGroup(sigs);
        var mergedBuckets = MergeSameGroupBuckets(buckets);
        return mergedBuckets.Select(b => BuildOverride(b, allEpisodes)).ToList();
    }

    /// <summary>
    /// Groups pre-resolved place records into release candidates.
    /// </summary>
    public IReadOnlyList<VideoReleaseCandidate> Group(IEnumerable<ResolvedVideoPlace> resolved)
    {
        var sigs = resolved.Select(BuildSignature).ToList();

        // Full episode set across all files — used to filter incomplete candidates
        var allEpisodes = sigs.SelectMany(s => s.EpisodeIds).ToHashSet();

        var buckets = FuzzyGroup(sigs);

        // Merge buckets that share the same release group — quality variation within a group
        // (subtitle count, chaptered vs. unchaptered per episode) is expressed as mixed signals
        // on the combined candidate rather than splitting into separate candidates.
        var mergedBuckets = MergeSameGroupBuckets(buckets);

        // Phase 2: version-strategy candidates per merged family
        var specs = mergedBuckets.SelectMany(GenerateVersionStrategyCandidates).ToList();

        // Phase 3: gap-fill candidates — one per (partial anchor, filler group) pair
        var gapFillSpecs = GenerateGapFillCandidates(specs, allEpisodes).ToList();

        // Build candidates, dedup by file set, and filter out any that don't cover
        // every known episode — selecting such a candidate would leave gaps.
        var seenFileSets = new HashSet<string>();
        var result = new List<VideoReleaseCandidate>();
        foreach (var spec in specs.Concat(gapFillSpecs))
        {
            if (allEpisodes.Count > 0)
            {
                var specEpisodes = spec.Files.SelectMany(f => f.EpisodeIds).ToHashSet();
                if (!specEpisodes.IsSupersetOf(allEpisodes)) continue;
            }

            var fileSetKey = string.Join(",", spec.Files.Select(f => f.Place.ID).OrderBy(id => id));
            if (seenFileSets.Add(fileSetKey))
                result.Add(BuildCandidate(spec));
        }
        return result;
    }

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
        if (isChaptered is null && (media as IMediaInfo)?.Chapters.Count > 0)
            isChaptered = true;

        var episodeIds = sri?.CrossReferences
            .Select(x => (
                x is EmbeddedCrossReference ecr ? ecr.EpisodeType : EpisodeType.Episode,
                x.AnidbEpisodeID))
            .Where(k => k.Item2 > 0)
            .ToHashSet() ?? new HashSet<(EpisodeType, int)>();

        var parentDirectory = GetParentDirectory(place.RelativePath);
        return new FileSignature
        {
            Place = place,
            ParentDirectory = parentDirectory,
            ReleaseInfo = sri,
            AudioLanguages = audioLangs,
            AudioLanguageSet = audioLangs.ToHashSet(),
            SubtitleLanguages = subLangs,
            SubtitleLanguageSet = subLangs.ToHashSet(),
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
    /// </summary>
    private static List<List<FileSignature>> FuzzyGroup(List<FileSignature> signatures)
    {
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
    /// Missing values on either side are treated as wildcards.
    /// </summary>
    private static bool AreCompatible(FileSignature a, FileSignature b)
    {
        // Hard separators — always split regardless of missing data
        if (a.Place.ManagedFolderID != b.Place.ManagedFolderID) return false;
        if (a.ParentDirectory != b.ParentDirectory) return false;

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

        // Language sets — only conflict when both sides have at least one identified language and the sets differ.
        if (a.AudioLanguageSet.Count > 0 && b.AudioLanguageSet.Count > 0 && !a.AudioLanguageSet.SetEquals(b.AudioLanguageSet)) return false;
        if (a.SubtitleLanguageSet.Count > 0 && b.SubtitleLanguageSet.Count > 0 && !a.SubtitleLanguageSet.SetEquals(b.SubtitleLanguageSet)) return false;

        // Audio codec and container — soft separators
        if (!NullOrCompatible(a.AudioCodec, b.AudioCodec)) return false;
        if (!NullOrCompatible(a.Container, b.Container)) return false;

        return true;
    }

    /// <summary>
    /// Merges FuzzyGroup buckets that share the same release-group key (GroupID + GroupSource)
    /// and cover disjoint sets of episodes into a single combined bucket.
    /// <para>
    /// Episode overlap is the signal that two same-group buckets are genuinely different products
    /// (e.g., a 1080p and a 720p encode both covering episode 1). Non-overlapping buckets are
    /// incidental quality variation within one release (e.g., chaptered eps 1–10 alongside
    /// unchaptered eps 11–12, or BD eps 1–12 alongside a DVD-only ep 13).
    /// </para>
    /// Buckets without a known group key, or without any episode data, are left as-is.
    /// </summary>
    private static List<List<FileSignature>> MergeSameGroupBuckets(List<List<FileSignature>> buckets)
    {
        var result = new List<List<FileSignature>>();

        foreach (var bucket in buckets)
        {
            var groupKey = bucket.Select(s => GroupKey(s.ReleaseInfo)).FirstOrDefault(k => k is not null);
            if (groupKey is null)
            {
                result.Add(bucket);
                continue;
            }

            var bucketEps = bucket.SelectMany(s => s.EpisodeIds).ToHashSet();

            // Without episode data we cannot verify that the episodes don't overlap — keep separate.
            if (bucketEps.Count > 0)
            {
                var target = result.FirstOrDefault(r =>
                {
                    var rKey = r.Select(s => GroupKey(s.ReleaseInfo)).FirstOrDefault(k => k is not null);
                    if (rKey != groupKey) return false;
                    var rEps = r.SelectMany(s => s.EpisodeIds).ToHashSet();
                    return rEps.Count > 0 && !rEps.Overlaps(bucketEps);
                });

                if (target is not null)
                {
                    target.AddRange(bucket);
                    continue;
                }
            }

            result.Add(new List<FileSignature>(bucket));
        }

        return result;
    }

    /// <summary>
    /// For each FuzzyGroup bucket, generates one or more version-strategy candidates:
    /// BestAvailable (highest version per episode) and Consistent(v) for each older version
    /// when version-colliding episodes exist.
    /// </summary>
    private static IEnumerable<CandidateSpec> GenerateVersionStrategyCandidates(List<FileSignature> signatures)
    {
        // Without episode data on every SRI-backed file we cannot determine version collisions
        if (signatures.Any(s => s.ReleaseInfo is null || s.EpisodeIds.Count == 0))
        {
            yield return new CandidateSpec(signatures, ReleaseVersionStrategy.BestAvailable,
                MaxVersion(signatures), false, []);
            yield break;
        }

        // Map each episode to the set of (version, sig) pairs covering it
        var episodeToVersionedFiles = signatures
            .SelectMany(s => s.EpisodeIds.Select(ep => (ep, Version: s.ReleaseInfo!.Version, Sig: s)))
            .GroupBy(x => x.ep)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (x.Version, x.Sig)).ToList());

        // Version-colliding episodes: same episode, different version numbers
        var collidingEpisodes = episodeToVersionedFiles
            .Where(kv => kv.Value.Select(x => x.Version).Distinct().Count() > 1)
            .Select(kv => kv.Key)
            .ToHashSet();

        if (collidingEpisodes.Count == 0)
        {
            yield return new CandidateSpec(signatures, ReleaseVersionStrategy.BestAvailable,
                MaxVersion(signatures), false, []);
            yield break;
        }

        var maxVersion = MaxVersion(signatures);

        // BestAvailable: for each colliding episode, keep only the highest-version file(s)
        var bestAvailableFiles = SelectFilesForStrategy(episodeToVersionedFiles, collidingEpisodes, bestAvailable: true);
        yield return new CandidateSpec(bestAvailableFiles, ReleaseVersionStrategy.BestAvailable, maxVersion, false, []);

        // Consistent(v) for each distinct version below maxVersion
        var distinctVersionsBeforeMax = signatures
            .Where(s => (s.ReleaseInfo?.Version ?? 0) > 0)
            .Select(s => s.ReleaseInfo!.Version)
            .Distinct()
            .Where(v => v < maxVersion)
            .OrderBy(v => v);

        foreach (var version in distinctVersionsBeforeMax)
        {
            var consistentFiles = SelectFilesForStrategy(episodeToVersionedFiles, collidingEpisodes,
                bestAvailable: false, targetVersion: version);
            if (consistentFiles.Count == 0) continue;
            yield return new CandidateSpec(consistentFiles, ReleaseVersionStrategy.Consistent, version, false, []);
        }
    }

    /// <summary>
    /// For each version-strategy spec with partial episode coverage, generates a single
    /// gap-fill candidate per distinct filler group that can cover the gap completely.
    /// The filler always uses BestAvailable files, restricted to the gap episodes.
    /// </summary>
    private static IEnumerable<CandidateSpec> GenerateGapFillCandidates(
        IReadOnlyList<CandidateSpec> specs, IReadOnlySet<(EpisodeType, int)> allEpisodes)
    {
        if (allEpisodes.Count == 0) yield break;

        // Pre-index the best spec per group to use as a filler source.
        // BestAvailable is preferred; fall back to any strategy when absent.
        var bestSpecByGroup = specs
            .GroupBy(s => s.Files.Select(f => GroupKey(f.ReleaseInfo)).FirstOrDefault(k => k is not null))
            .Where(g => g.Key is not null)
            .ToDictionary(
                g => g.Key!,
                g => g.FirstOrDefault(s => s.Strategy == ReleaseVersionStrategy.BestAvailable) ?? g.First());

        foreach (var anchor in specs)
        {
            var anchorEps = anchor.Files.SelectMany(f => f.EpisodeIds).ToHashSet();
            if (anchorEps.Count == 0 || anchorEps.IsSupersetOf(allEpisodes)) continue;

            var gapEps = allEpisodes.Except(anchorEps).ToHashSet();

            var anchorGroupKey = anchor.Files
                .Select(f => GroupKey(f.ReleaseInfo))
                .FirstOrDefault(k => k is not null);

            foreach (var (fillerGroupKey, fillerSpec) in bestSpecByGroup)
            {
                if (fillerGroupKey == anchorGroupKey) continue;

                var fillerFiles = fillerSpec.Files
                    .Where(f => f.EpisodeIds.Any(ep => gapEps.Contains(ep)))
                    .ToList();
                if (fillerFiles.Count == 0) continue;

                // Only generate the candidate when this filler can cover every gap episode.
                var fillerEps = fillerFiles.SelectMany(f => f.EpisodeIds).ToHashSet();
                if (!fillerEps.IsSupersetOf(gapEps)) continue;

                var fillerGroupShortName = fillerFiles
                    .Select(f => f.ReleaseInfo?.GroupShortName)
                    .FirstOrDefault(n => n is not null);

                IReadOnlyList<string> secondaryGroups = fillerGroupShortName is not null
                    ? [fillerGroupShortName]
                    : [];

                yield return new CandidateSpec(
                    anchor.Files.Concat(fillerFiles).ToList(),
                    anchor.Strategy,
                    anchor.Version,
                    IsMixed: true,
                    SecondaryGroupShortNames: secondaryGroups);
            }
        }
    }

    /// <summary>
    /// Selects files for a version strategy. For BestAvailable, picks the highest-version
    /// file per colliding episode (tiebreaker: prefer non-corrupted). For Consistent,
    /// picks files of a specific target version. Non-colliding episodes always pass through.
    /// </summary>
    private static List<FileSignature> SelectFilesForStrategy(
        Dictionary<(EpisodeType, int), List<(int Version, FileSignature Sig)>> episodeToVersionedFiles,
        HashSet<(EpisodeType, int)> collidingEpisodes,
        bool bestAvailable,
        int targetVersion = 0)
    {
        var selectedPlaceIds = new HashSet<int>();
        var selectedSigs = new List<FileSignature>();

        foreach (var (ep, epFiles) in episodeToVersionedFiles)
        {
            List<FileSignature> chosen;
            if (collidingEpisodes.Contains(ep))
            {
                if (bestAvailable)
                {
                    var epMax = epFiles.Max(x => x.Version);
                    var maxVersionFiles = epFiles.Where(x => x.Version == epMax).ToList();
                    // Tiebreaker: prefer non-corrupted when versions match
                    var nonCorrupted = maxVersionFiles.Where(x => x.Sig.ReleaseInfo?.IsCorrupted != true).ToList();
                    chosen = (nonCorrupted.Count > 0 ? nonCorrupted : maxVersionFiles).Select(x => x.Sig).ToList();
                }
                else
                {
                    chosen = epFiles.Where(x => x.Version == targetVersion).Select(x => x.Sig).ToList();
                }
            }
            else
            {
                // Non-colliding: include all files for this episode in every strategy
                chosen = epFiles.Select(x => x.Sig).ToList();
            }

            foreach (var sig in chosen)
            {
                if (selectedPlaceIds.Add(sig.Place.ID))
                    selectedSigs.Add(sig);
            }
        }

        return selectedSigs;
    }

    private static VideoReleaseOverride BuildOverride(
        List<FileSignature> signatures, IReadOnlySet<(EpisodeType, int)> allEpisodes)
    {
        var rep = signatures.FirstOrDefault(s => s.ReleaseInfo is not null) ?? signatures[0];
        var sri = rep.ReleaseInfo;

        var source = Majority(signatures.Select(s =>
            s.ReleaseInfo?.Source is { } src and not ReleaseSource.Unknown ? (ReleaseSource?)src : null))
            ?? ReleaseSource.Unknown;

        var bucketEps = signatures.SelectMany(s => s.EpisodeIds).ToHashSet();

        var files = signatures
            .Select(s => new VideoReleaseOverrideFile
            {
                Place = s.Place,
                Version = s.ReleaseInfo?.Version ?? 0,
                IsChaptered = s.IsChaptered,
                SubtitleStreamCount = s.SubtitleStreamCount,
                EpisodeIds = s.EpisodeIds,
            })
            .ToList();

        return new VideoReleaseOverride
        {
            GroupID = sri?.GroupID,
            GroupSource = sri?.GroupSource,
            GroupName = sri?.GroupName,
            GroupShortName = sri?.GroupShortName,
            Source = source,
            Resolution = MajorityRef(signatures.Select(s => s.Resolution)),
            VideoCodec = MajorityRef(signatures.Select(s => s.VideoCodec)),
            BitDepth = Majority(signatures.Select(s => s.BitDepth > 0 ? (int?)s.BitDepth : null)) ?? 0,
            AudioCodec = MajorityRef(signatures.Select(s => s.AudioCodec)),
            AudioStreamCount = Majority(signatures.Select(s => s.AudioStreamCount > 0 ? (int?)s.AudioStreamCount : null)) ?? 0,
            SubtitleStreamCount = Majority(signatures.Select(s => s.SubtitleStreamCount > 0 ? (int?)s.SubtitleStreamCount : null)) ?? 0,
            AudioLanguages = MajorityLanguageSet(signatures.Select(s => s.AudioLanguages)),
            SubtitleLanguages = MajorityLanguageSet(signatures.Select(s => s.SubtitleLanguages)),
            HasPartialCoverage = allEpisodes.Count > 0 && !bucketEps.IsSupersetOf(allEpisodes),
            Files = files,
        };
    }

    private static VideoReleaseCandidate BuildCandidate(CandidateSpec spec)
    {
        var signatures = spec.Files;
        var rep = signatures.FirstOrDefault(s => s.ReleaseInfo is not null) ?? signatures[0];
        var sri = rep.ReleaseInfo;

        var videoCodec          = MajorityRef(signatures.Select(s => s.VideoCodec));
        var resolution          = MajorityRef(signatures.Select(s => s.Resolution));
        var bitDepth            = Majority(signatures.Select(s => s.BitDepth > 0 ? (int?)s.BitDepth : null)) ?? 0;
        var audioCodec          = MajorityRef(signatures.Select(s => s.AudioCodec));
        var container           = MajorityRef(signatures.Select(s => s.Container));
        var audioStreamCount    = Majority(signatures.Select(s => s.AudioStreamCount > 0 ? (int?)s.AudioStreamCount : null)) ?? 0;
        var subtitleStreamCount = Majority(signatures.Select(s => s.SubtitleStreamCount > 0 ? (int?)s.SubtitleStreamCount : null)) ?? 0;
        var source              = Majority(signatures.Select(s => s.ReleaseInfo?.Source is { } src and not ReleaseSource.Unknown ? (ReleaseSource?)src : null))
                                  ?? ReleaseSource.Unknown;
        var audioLanguages      = MajorityLanguageSet(signatures.Select(s => s.AudioLanguages));
        var subtitleLanguages   = MajorityLanguageSet(signatures.Select(s => s.SubtitleLanguages));

        // Majority+mixed for quality flags
        var (isChaptered, isChapteredMixed)   = MajorityWithMix(signatures.Select(s => s.IsChaptered));
        var (isCensored, isCensoredMixed)     = MajorityWithMix(signatures.Select(s => s.ReleaseInfo?.IsCensored));
        var (isCreditless, isCreditlessMixed) = MajorityWithMix(signatures.Select(s => s.ReleaseInfo?.IsCreditless));

        // Any-true for corruption — one corrupt file contaminates the whole candidate
        var isCorrupted = signatures.Any(s => s.ReleaseInfo?.IsCorrupted == true);

        // Homogeneous when all files with a known group share the same group key
        var distinctGroupKeys = signatures
            .Select(s => GroupKey(s.ReleaseInfo))
            .Where(k => k is not null)
            .Distinct()
            .Count();
        var isHomogeneous = distinctGroupKeys <= 1;

        var episodeCoverage = signatures.SelectMany(s => s.EpisodeIds).ToHashSet();

        var sortedAudio          = string.Join(",", audioLanguages.Select(l => l.GetString()).Order());
        var sortedSubs           = string.Join(",", subtitleLanguages.Select(l => l.GetString()).Order());
        var sortedSecondaryGroups = string.Join(",", spec.SecondaryGroupShortNames.Order());

        var key = ComputeKey(
            sri?.GroupID, sri?.GroupSource,
            source, sortedAudio, sortedSubs,
            videoCodec, bitDepth, resolution,
            audioCodec, container,
            audioStreamCount, subtitleStreamCount,
            isChaptered, isCensored, isCreditless,
            isCorrupted, spec.Version,
            spec.Strategy, spec.IsMixed, sortedSecondaryGroups);

        var placeSignals = signatures.ToDictionary(
            s => s.Place.ID,
            s => new PlaceQualitySignals(
                s.IsChaptered,
                s.ReleaseInfo?.IsCensored,
                s.ReleaseInfo?.IsCreditless,
                s.ReleaseInfo?.IsCorrupted ?? false,
                s.AudioLanguages,
                s.SubtitleLanguages));

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
            IsChapteredMixed = isChapteredMixed,
            IsCensored = isCensored,
            IsCensoredMixed = isCensoredMixed,
            IsCreditless = isCreditless,
            IsCreditlessMixed = isCreditlessMixed,
            IsCorrupted = isCorrupted,
            Version = spec.Version,
            VersionStrategy = spec.Strategy,
            IsMixed = spec.IsMixed,
            IsHomogeneous = isHomogeneous,
            SecondaryGroupNames = spec.SecondaryGroupShortNames,
            Places = signatures.Select(s => s.Place).ToList(),
            PlaceSignals = placeSignals,
            EpisodeCoverage = episodeCoverage,
        };
    }

    private static int MaxVersion(IEnumerable<FileSignature> sigs)
        => sigs.Max(s => s.ReleaseInfo?.Version ?? 0);

    private static (bool? Majority, bool IsMixed) MajorityWithMix(IEnumerable<bool?> values)
    {
        var knownValues = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (knownValues.Count == 0) return (null, false);
        var trueCount = knownValues.Count(v => v);
        var falseCount = knownValues.Count - trueCount;
        return (trueCount >= falseCount, trueCount > 0 && falseCount > 0);
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

    private static string ComputeKey(
        string? groupId, string? groupSource,
        ReleaseSource source,
        string sortedAudio, string sortedSubs,
        string? videoCodec, int bitDepth, string? resolution,
        string? audioCodec, string? container,
        int audioStreamCount, int subtitleStreamCount,
        bool? isChaptered, bool? isCensored, bool? isCreditless,
        bool isCorrupted, int version,
        ReleaseVersionStrategy strategy, bool isMixed, string sortedSecondaryGroups)
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
            version,
            (int)strategy,
            isMixed ? "1" : "0",
            sortedSecondaryGroups
        );
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(signal));
        return Convert.ToHexStringLower(hash);
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

    private static string GetParentDirectory(string relativePath)
    {
        var lastSlash = relativePath.LastIndexOf('/');
        return lastSlash <= 0 ? string.Empty : relativePath[..lastSlash];
    }

    private sealed record CandidateSpec(
        List<FileSignature> Files,
        ReleaseVersionStrategy Strategy,
        int Version,
        bool IsMixed,
        IReadOnlyList<string> SecondaryGroupShortNames);

    private sealed class FileSignature
    {
        public required VideoLocal_Place Place { get; init; }
        public required string ParentDirectory { get; init; }
        public StoredReleaseInfo? ReleaseInfo { get; init; }
        public required IReadOnlyList<TitleLanguage> AudioLanguages { get; init; }
        public required IReadOnlySet<TitleLanguage> AudioLanguageSet { get; init; }
        public required IReadOnlyList<TitleLanguage> SubtitleLanguages { get; init; }
        public required IReadOnlySet<TitleLanguage> SubtitleLanguageSet { get; init; }
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
