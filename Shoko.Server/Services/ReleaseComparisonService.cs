using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
/// Ranks <see cref="VideoReleaseCandidate"/> objects using a configurable sequential
/// tie-breaker comparison: the first signal in <see cref="ReleaseComparisonPreferences.SignalPriority"/>
/// where the two candidates differ declares the winner.
/// </summary>
public class ReleaseComparisonService(ISettingsProvider settingsProvider, VideoReleaseGroupingService groupingService)
{
    private ReleaseComparisonPreferences Prefs => settingsProvider.GetSettings().ReleaseComparisonPreferences;

    /// <summary>
    /// Compares two candidates. Returns -1 if <paramref name="a"/> is preferred,
    /// 1 if <paramref name="b"/> is preferred, or 0 if they are equivalent on all
    /// configured signals.
    /// </summary>
    public int Compare(VideoReleaseCandidate a, VideoReleaseCandidate b)
        => CompareWithDecision(a, b).Result;

    /// <summary>
    /// Returns the candidates sorted best-first. Stable: ties preserve input order.
    /// </summary>
    public IReadOnlyList<VideoReleaseCandidate> Rank(IEnumerable<VideoReleaseCandidate> candidates)
    {
        var list = candidates.ToList();
        // Stable sort: preserve input order for ties
        list.Sort((a, b) => Compare(a, b));
        return list;
    }

    /// <summary>
    /// Sorts <paramref name="videos"/> by release rank (rank 1 first). Videos not
    /// captured by any candidate are appended at the end in their original order.
    /// </summary>
    public List<VideoLocal> SortByRank(IEnumerable<VideoLocal> videos)
    {
        var list = videos.ToList();
        var byId = list.ToDictionary(v => v.VideoLocalID);
        var places = list.Select(v => v.Places.FirstOrDefault()).OfType<VideoLocal_Place>().ToList();
        var ranked = Rank(groupingService.Group(places));
        var seen = new HashSet<int>();
        var sorted = new List<VideoLocal>(list.Count);
        foreach (var candidate in ranked)
            foreach (var place in candidate.Places)
                if (byId.TryGetValue(place.VideoID, out var video) && seen.Add(video.VideoLocalID))
                    sorted.Add(video);
        foreach (var video in list)
            if (seen.Add(video.VideoLocalID))
                sorted.Add(video);
        return sorted;
    }

    /// <summary>
    /// Returns the subset of <paramref name="primary"/>'s episode coverage that it is
    /// actually trusted to use for automatic redundancy ("trumping" other candidates).
    /// A primary that is a gap-fill/mixed composite, is missing release info, is marked
    /// corrupted, or internally disagrees on chapter/censor/creditless status is not
    /// trustworthy enough to auto-delete other candidates against — this returns an
    /// empty set in that case (or, under <see cref="EpisodeTypeScope.BestPerType"/>,
    /// excludes only the episode types where the disqualifying condition applies).
    /// </summary>
    public IReadOnlySet<(EpisodeType, int)> GetEligiblePrimaryCoverage(VideoReleaseCandidate primary)
    {
        // These disagreement flags have no per-type equivalent, so they always deny
        // trumping outright regardless of EpisodeTypeScope.
        if (primary.IsChapteredMixed || primary.IsCensoredMixed || primary.IsCreditlessMixed)
            return new HashSet<(EpisodeType, int)>();

        if (Prefs.EpisodeTypeScope == EpisodeTypeScope.KeepTogether)
        {
            if (primary.IsMixed || !primary.HasReleaseInfo || primary.IsCorrupted)
                return new HashSet<(EpisodeType, int)>();
            return primary.EpisodeCoverage;
        }

        // BestPerType: a primary can be a single, complete, non-corrupted release for
        // one episode type while being mixed/incomplete/corrupted for another — evaluate
        // "single release" and "has info" independently per type via EpisodeGroupMap
        // (guaranteed to have an entry for every covered episode), and reuse the
        // per-type IsCorrupted signal already computed on TypeSignals.
        var result = new HashSet<(EpisodeType, int)>();
        foreach (var typeGroup in primary.EpisodeGroupMap.GroupBy(kv => kv.Key.Item1))
        {
            if (primary.TypeSignals.TryGetValue(typeGroup.Key, out var typeSignals) && typeSignals.IsCorrupted)
                continue;

            var groups = typeGroup.Select(kv => kv.Value).ToList();
            if (groups.Any(g => g is null))
                continue;
            if (groups.Distinct().Count() > 1)
                continue;

            foreach (var kv in typeGroup)
                result.Add(kv.Key);
        }
        return result;
    }

    /// <summary>
    /// Given the trusted coverage set of a primary candidate, returns the secondary
    /// candidates from <paramref name="candidates"/> whose entire episode coverage is
    /// already covered by <paramref name="primaryCoverage"/>. These are safe to delete
    /// without losing any episode. <paramref name="candidates"/> should not include the
    /// primary itself.
    /// </summary>
    public IReadOnlyList<VideoReleaseCandidate> GetRedundantCandidates(
        IReadOnlySet<(EpisodeType, int)> primaryCoverage,
        IReadOnlyList<VideoReleaseCandidate> candidates)
    {
        if (primaryCoverage.Count == 0)
            return [];

        var redundant = new List<VideoReleaseCandidate>();
        foreach (var candidate in candidates)
        {
            // If the candidate has no episode data we cannot verify coverage overlap —
            // keep it rather than risk deleting files that may cover episodes the
            // primary does not.
            if (candidate.EpisodeCoverage.Count == 0)
                continue;

            // Redundant if the primary covers every episode this candidate covers.
            if (candidate.EpisodeCoverage.IsSubsetOf(primaryCoverage))
                redundant.Add(candidate);
        }
        return redundant;
    }

    /// <summary>
    /// Returns the places from <paramref name="secondaryPlaces"/> whose individual episode
    /// coverage — determined by calling <paramref name="getCoverage"/> per place —
    /// is a subset of <paramref name="primaryCoverage"/>. These files are individually
    /// redundant: the primary already provides a higher-ranked alternative for every
    /// episode they cover.
    /// Places whose coverage is empty (unknown, no SRI) are always retained.
    /// </summary>
    public IReadOnlyList<VideoLocal_Place> GetRedundantPlaces(
        IReadOnlySet<(EpisodeType, int)> primaryCoverage,
        IReadOnlyCollection<VideoLocal_Place> secondaryPlaces,
        Func<VideoLocal_Place, IReadOnlySet<(EpisodeType, int)>> getCoverage)
    {
        if (primaryCoverage.Count == 0)
            return [];

        var result = new List<VideoLocal_Place>();
        foreach (var place in secondaryPlaces)
        {
            var coverage = getCoverage(place);
            if (coverage.Count > 0 && coverage.IsSubsetOf(primaryCoverage))
                result.Add(place);
        }
        return result;
    }

    /// <summary>
    /// Result of <see cref="CompareWithDecision"/>: comparison result plus context for the UI.
    /// <see cref="DecidingSignal"/> is null when all configured signals are tied.
    /// <see cref="DecidingType"/> is the episode type that was decisive under
    /// <c>EpisodeTypeScope.BestPerType</c>; null for <c>KeepTogether</c> comparisons.
    /// <see cref="PrimaryValue"/> / <see cref="RunnerUpValue"/> hold the winning and losing display
    /// values for the deciding signal (null when signal is null or values are unknown).
    /// </summary>
    public record CompareDecision(
        int Result,
        ReleaseSignalType? DecidingSignal,
        EpisodeType? DecidingType,
        string? PrimaryValue,
        string? RunnerUpValue);

    /// <summary>
    /// Like <see cref="Compare"/> but also returns which signal decided the outcome,
    /// which episode type (under <c>BestPerType</c>), and display values for the UI.
    /// <see cref="CompareDecision.PrimaryValue"/> always describes the winner.
    /// </summary>
    public CompareDecision CompareWithDecision(VideoReleaseCandidate a, VideoReleaseCandidate b)
    {
        var prefs = Prefs;

        // Under KeepTogether, homogeneous (single-group) candidates always rank above
        // gap-fills before any quality signal is consulted.
        if (prefs.EpisodeTypeScope == EpisodeTypeScope.KeepTogether)
        {
            var homResult = CompareHomogeneity(a, b);
            if (homResult != 0)
            {
                var (aH, bH) = (a.IsHomogeneous.ToString(), b.IsHomogeneous.ToString());
                var (pHom, rHom) = homResult < 0 ? (aH, bH) : (bH, aH);
                return new CompareDecision(homResult, ReleaseSignalType.GroupHomogeneity, null, pHom, rHom);
            }
        }

        // Under BestPerType, compare quality per episode type for contested types
        // (types where both candidates have files). Episode types are compared in
        // ascending enum order so regular episodes (lowest value) come first.
        var contestedTypes = prefs.EpisodeTypeScope == EpisodeTypeScope.BestPerType
            ? a.TypeSignals.Keys
                .Intersect(b.TypeSignals.Keys)
                .OrderBy(t => (int)t)
                .ToList()
            : null;

        foreach (var signal in prefs.SignalPriority)
        {
            // GroupHomogeneity is now a structural rule (above), not a quality signal.
            if (signal == ReleaseSignalType.GroupHomogeneity)
                continue;

            // BestPerType: try each contested episode type before falling through
            // to the aggregate comparison below.
            if (contestedTypes is { Count: > 0 })
            {
                foreach (var type in contestedTypes)
                {
                    if (!a.TypeSignals.TryGetValue(type, out var aSig) ||
                        !b.TypeSignals.TryGetValue(type, out var bSig))
                        continue;
                    var typeResult = CompareSignalForType(signal, aSig, bSig, prefs);
                    if (typeResult != 0)
                    {
                        var (aT, bT) = GetTypeSignalDisplayValues(signal, aSig, bSig);
                        var (pT, rT) = typeResult < 0 ? (aT, bT) : (bT, aT);
                        return new CompareDecision(typeResult, signal, type, pT, rT);
                    }
                }
            }

            // Aggregate comparison — always used for KeepTogether, and as a fallback
            // for BestPerType signals not tracked per-type (SubGroup, Version).
            var result = CompareSignalAggregate(signal, a, b, prefs);
            if (result == 0) continue;

            var (aDisplay, bDisplay) = GetAggregateSignalDisplayValues(signal, a, b);
            var (primaryVal, runnerUpVal) = result < 0 ? (aDisplay, bDisplay) : (bDisplay, aDisplay);
            return new CompareDecision(result, signal, null, primaryVal, runnerUpVal);
        }
        return new CompareDecision(0, null, null, null, null);
    }

    /// <summary>Compares a single quality signal between two candidates using aggregate signals.</summary>
    private static int CompareSignalAggregate(
        ReleaseSignalType signal, VideoReleaseCandidate a, VideoReleaseCandidate b,
        ReleaseComparisonPreferences prefs) => signal switch
    {
        ReleaseSignalType.Source          => CompareSource(a, b, prefs),
        ReleaseSignalType.Resolution      => CompareResolution(a, b, prefs),
        ReleaseSignalType.VideoCodec      => CompareCodecList(a.VideoCodec, b.VideoCodec, prefs.VideoCodecOrder),
        ReleaseSignalType.BitDepth        => CompareBitDepth(a, b, prefs),
        ReleaseSignalType.AudioStreams     => CompareHigherInt(a.AudioStreamCount, b.AudioStreamCount),
        ReleaseSignalType.SubtitleStreams  => CompareHigherInt(a.SubtitleStreamCount, b.SubtitleStreamCount),
        ReleaseSignalType.AudioCodec      => CompareCodecList(a.AudioCodec, b.AudioCodec, prefs.AudioCodecOrder),
        ReleaseSignalType.AudioLanguage   => CompareLanguageList(a.AudioLanguages, b.AudioLanguages, prefs.AudioLanguageOrder),
        ReleaseSignalType.SubtitleLanguage => CompareLanguageList(a.SubtitleLanguages, b.SubtitleLanguages, prefs.SubtitleLanguageOrder),
        ReleaseSignalType.Chaptered       => CompareChapters(a, b),
        ReleaseSignalType.SubGroup        => CompareSubGroup(a, b, prefs),
        ReleaseSignalType.Version         => CompareHigherInt(a.Version, b.Version),
        ReleaseSignalType.Corrupted       => CompareCorrupted(a, b),
        ReleaseSignalType.Censored        => CompareCensored(a, b),
        ReleaseSignalType.Creditless      => CompareCreditless(a, b),
        _ => 0,
    };

    /// <summary>
    /// Compares a single quality signal between two <see cref="EpisodeTypeQualitySignals"/>
    /// instances (the per-type quality view). Signals that have no per-type representation
    /// (SubGroup, Version, GroupHomogeneity) return 0 here; they are handled at the
    /// aggregate level instead.
    /// </summary>
    private static int CompareSignalForType(
        ReleaseSignalType signal, EpisodeTypeQualitySignals a, EpisodeTypeQualitySignals b,
        ReleaseComparisonPreferences prefs) => signal switch
    {
        ReleaseSignalType.Source          => CompareCodecList(ReleaseSourceToString(a.Source), ReleaseSourceToString(b.Source), prefs.SourceOrder),
        ReleaseSignalType.Resolution      => CompareCodecList(a.Resolution, b.Resolution, prefs.ResolutionOrder),
        ReleaseSignalType.VideoCodec      => CompareCodecList(a.VideoCodec, b.VideoCodec, prefs.VideoCodecOrder),
        ReleaseSignalType.BitDepth        => CompareTypeBitDepth(a.BitDepth, b.BitDepth, prefs),
        ReleaseSignalType.AudioStreams     => CompareHigherInt(a.AudioStreamCount, b.AudioStreamCount),
        ReleaseSignalType.SubtitleStreams  => CompareHigherInt(a.SubtitleStreamCount, b.SubtitleStreamCount),
        ReleaseSignalType.AudioCodec      => CompareCodecList(a.AudioCodec, b.AudioCodec, prefs.AudioCodecOrder),
        ReleaseSignalType.AudioLanguage   => CompareLanguageList(a.AudioLanguages, b.AudioLanguages, prefs.AudioLanguageOrder),
        ReleaseSignalType.SubtitleLanguage => CompareLanguageList(a.SubtitleLanguages, b.SubtitleLanguages, prefs.SubtitleLanguageOrder),
        ReleaseSignalType.Chaptered       => CompareNullableBool(a.IsChaptered, b.IsChaptered),
        ReleaseSignalType.Corrupted       => CompareCorruptedBool(a.IsCorrupted, b.IsCorrupted),
        ReleaseSignalType.Censored        => CompareCensoredBool(a.IsCensored, b.IsCensored),
        ReleaseSignalType.Creditless      => CompareNullableBool(a.IsCreditless, b.IsCreditless),
        _ => 0,
    };

    private static (string? AValue, string? BValue) GetAggregateSignalDisplayValues(
        ReleaseSignalType signal, VideoReleaseCandidate a, VideoReleaseCandidate b)
        => signal switch
        {
            ReleaseSignalType.Source          => (ReleaseSourceToString(a.Source), ReleaseSourceToString(b.Source)),
            ReleaseSignalType.Resolution      => (a.Resolution, b.Resolution),
            ReleaseSignalType.VideoCodec      => (a.VideoCodec, b.VideoCodec),
            ReleaseSignalType.BitDepth        => (a.BitDepth > 0 ? a.BitDepth.ToString() : null, b.BitDepth > 0 ? b.BitDepth.ToString() : null),
            ReleaseSignalType.AudioStreams     => (a.AudioStreamCount > 0 ? a.AudioStreamCount.ToString() : null, b.AudioStreamCount > 0 ? b.AudioStreamCount.ToString() : null),
            ReleaseSignalType.SubtitleStreams  => (a.SubtitleStreamCount > 0 ? a.SubtitleStreamCount.ToString() : null, b.SubtitleStreamCount > 0 ? b.SubtitleStreamCount.ToString() : null),
            ReleaseSignalType.AudioCodec      => (a.AudioCodec, b.AudioCodec),
            ReleaseSignalType.AudioLanguage   => (LanguageListDisplay(a.AudioLanguages), LanguageListDisplay(b.AudioLanguages)),
            ReleaseSignalType.SubtitleLanguage => (LanguageListDisplay(a.SubtitleLanguages), LanguageListDisplay(b.SubtitleLanguages)),
            ReleaseSignalType.Chaptered       => (a.IsChaptered?.ToString(), b.IsChaptered?.ToString()),
            ReleaseSignalType.GroupHomogeneity => (a.IsHomogeneous.ToString(), b.IsHomogeneous.ToString()),
            ReleaseSignalType.SubGroup        => (GroupDisplay(a), GroupDisplay(b)),
            ReleaseSignalType.Version         => (a.Version > 0 ? a.Version.ToString() : null, b.Version > 0 ? b.Version.ToString() : null),
            ReleaseSignalType.Corrupted       => ((!a.IsCorrupted).ToString(), (!b.IsCorrupted).ToString()),
            ReleaseSignalType.Censored        => (a.IsCensored is null ? null : (!a.IsCensored.Value).ToString(), b.IsCensored is null ? null : (!b.IsCensored.Value).ToString()),
            ReleaseSignalType.Creditless      => (a.IsCreditless?.ToString(), b.IsCreditless?.ToString()),
            _ => (null, null),
        };

    private static (string? AValue, string? BValue) GetTypeSignalDisplayValues(
        ReleaseSignalType signal, EpisodeTypeQualitySignals a, EpisodeTypeQualitySignals b)
        => signal switch
        {
            ReleaseSignalType.Source          => (ReleaseSourceToString(a.Source), ReleaseSourceToString(b.Source)),
            ReleaseSignalType.Resolution      => (a.Resolution, b.Resolution),
            ReleaseSignalType.VideoCodec      => (a.VideoCodec, b.VideoCodec),
            ReleaseSignalType.BitDepth        => (a.BitDepth > 0 ? a.BitDepth.ToString() : null, b.BitDepth > 0 ? b.BitDepth.ToString() : null),
            ReleaseSignalType.AudioStreams     => (a.AudioStreamCount > 0 ? a.AudioStreamCount.ToString() : null, b.AudioStreamCount > 0 ? b.AudioStreamCount.ToString() : null),
            ReleaseSignalType.SubtitleStreams  => (a.SubtitleStreamCount > 0 ? a.SubtitleStreamCount.ToString() : null, b.SubtitleStreamCount > 0 ? b.SubtitleStreamCount.ToString() : null),
            ReleaseSignalType.AudioCodec      => (a.AudioCodec, b.AudioCodec),
            ReleaseSignalType.AudioLanguage   => (LanguageListDisplay(a.AudioLanguages), LanguageListDisplay(b.AudioLanguages)),
            ReleaseSignalType.SubtitleLanguage => (LanguageListDisplay(a.SubtitleLanguages), LanguageListDisplay(b.SubtitleLanguages)),
            ReleaseSignalType.Chaptered       => (a.IsChaptered?.ToString(), b.IsChaptered?.ToString()),
            ReleaseSignalType.Corrupted       => ((!a.IsCorrupted).ToString(), (!b.IsCorrupted).ToString()),
            ReleaseSignalType.Censored        => (a.IsCensored is null ? null : (!a.IsCensored.Value).ToString(), b.IsCensored is null ? null : (!b.IsCensored.Value).ToString()),
            ReleaseSignalType.Creditless      => (a.IsCreditless?.ToString(), b.IsCreditless?.ToString()),
            _ => (null, null),
        };

    private static string? LanguageListDisplay(IReadOnlyList<TitleLanguage> langs)
        => langs.Count > 0 ? string.Join(", ", langs.Select(l => l.ToString())) : null;

    private static string? GroupDisplay(VideoReleaseCandidate c)
    {
        if (string.IsNullOrEmpty(c.GroupName) && string.IsNullOrEmpty(c.GroupShortName)) return null;
        if (string.IsNullOrEmpty(c.GroupShortName) || c.GroupName == c.GroupShortName) return c.GroupName;
        if (string.IsNullOrEmpty(c.GroupName)) return c.GroupShortName;
        return $"{c.GroupName} ({c.GroupShortName})";
    }

    // ── per-signal comparison helpers ───────────────────────────────────────

    private static int CompareSource(VideoReleaseCandidate a, VideoReleaseCandidate b,
        ReleaseComparisonPreferences prefs)
    {
        var aStr = ReleaseSourceToString(a.Source);
        var bStr = ReleaseSourceToString(b.Source);
        return CompareCodecList(aStr, bStr, prefs.SourceOrder);
    }

    private static string? ReleaseSourceToString(ReleaseSource src) =>
        src switch
        {
            ReleaseSource.BluRay    => "BluRay",
            ReleaseSource.DVD       => "DVD",
            ReleaseSource.TV        => "TV",
            ReleaseSource.Web       => "Web",
            ReleaseSource.VHS       => "VHS",
            ReleaseSource.VCD       => "VCD",
            ReleaseSource.LaserDisc => "LaserDisc",
            ReleaseSource.Camera    => "Camera",
            ReleaseSource.Film      => "Film",
            _ => null,  // Unknown/Other → treat as unknown, skip
        };

    private static int CompareResolution(VideoReleaseCandidate a, VideoReleaseCandidate b,
        ReleaseComparisonPreferences prefs)
        => CompareCodecList(a.Resolution, b.Resolution, prefs.ResolutionOrder);

    /// <summary>
    /// Compares two values against an ordered preference list.
    /// Lower index = better. Both null → 0. One null → the other wins. Both absent from list → 0.
    /// </summary>
    private static int CompareCodecList(string? a, string? b, List<string> order)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
        if (string.IsNullOrEmpty(a)) return 1;   // b wins — a is unknown
        if (string.IsNullOrEmpty(b)) return -1;  // a wins — b is unknown

        var aIdx = order.FindIndex(s => string.Equals(s, a, StringComparison.OrdinalIgnoreCase));
        var bIdx = order.FindIndex(s => string.Equals(s, b, StringComparison.OrdinalIgnoreCase));

        // Both absent from preference list → no preference
        if (aIdx < 0 && bIdx < 0) return 0;
        if (aIdx < 0) return 1;   // b is in the list, a is not → b wins
        if (bIdx < 0) return -1;  // a is in the list, b is not → a wins

        // Lower index = better (earlier in the preference list)
        return aIdx.CompareTo(bIdx);
    }

    private static int CompareBitDepth(VideoReleaseCandidate a, VideoReleaseCandidate b,
        ReleaseComparisonPreferences prefs)
    {
        if (a.BitDepth == 0 || b.BitDepth == 0) return 0;
        if (a.BitDepth == b.BitDepth) return 0;
        var higherWins = prefs.PreferHigherBitDepth;
        return higherWins
            ? b.BitDepth.CompareTo(a.BitDepth)   // higher is better → a=10,b=8 → a wins → return -1
            : a.BitDepth.CompareTo(b.BitDepth);  // lower is better
    }

    /// <summary>Higher count wins. Zero on either side is treated as unknown (skip).</summary>
    private static int CompareHigherInt(int a, int b)
    {
        if (a == 0 || b == 0) return 0;
        return b.CompareTo(a);  // higher a → a wins → negative
    }

    /// <summary>
    /// Evaluates language preferences in order. For each preferred language, if
    /// one candidate has it and the other does not, the candidate with the language
    /// wins. Both or neither having a language is a tie on that entry; evaluation
    /// continues to the next. Empty preference list → always tied.
    /// </summary>
    private static int CompareLanguageList(
        IReadOnlyList<TitleLanguage> a, IReadOnlyList<TitleLanguage> b, List<string> order)
    {
        if (order.Count == 0) return 0;
        var aSet = new HashSet<string>(a.Select(l => l.ToString()), StringComparer.OrdinalIgnoreCase);
        var bSet = new HashSet<string>(b.Select(l => l.ToString()), StringComparer.OrdinalIgnoreCase);
        foreach (var lang in order)
        {
            var inA = aSet.Contains(lang);
            var inB = bSet.Contains(lang);
            if (inA && !inB) return -1;
            if (inB && !inA) return 1;
        }
        return 0;
    }

    private static int CompareChapters(VideoReleaseCandidate a, VideoReleaseCandidate b)
        => CompareNullableBool(a.IsChaptered, b.IsChaptered);

    private static int CompareNullableBool(bool? a, bool? b)
    {
        if (a is null || b is null) return 0;
        return (b == true ? 1 : 0).CompareTo(a == true ? 1 : 0);  // true wins
    }

    private static int CompareHomogeneity(VideoReleaseCandidate a, VideoReleaseCandidate b)
    {
        if (a.IsHomogeneous == b.IsHomogeneous) return 0;
        return a.IsHomogeneous ? -1 : 1;  // homogeneous (single-group) wins
    }

    private static int CompareTypeBitDepth(int a, int b, ReleaseComparisonPreferences prefs)
    {
        if (a == 0 || b == 0) return 0;
        if (a == b) return 0;
        return prefs.PreferHigherBitDepth ? b.CompareTo(a) : a.CompareTo(b);
    }

    private static int CompareCorruptedBool(bool a, bool b)
    {
        if (a == b) return 0;
        return a ? 1 : -1;  // not corrupted wins
    }

    private static int CompareCensoredBool(bool? a, bool? b)
    {
        if (a is null || b is null) return 0;
        if (a == b) return 0;
        return a == true ? 1 : -1;  // not censored wins
    }

    private static int CompareSubGroup(VideoReleaseCandidate a, VideoReleaseCandidate b,
        ReleaseComparisonPreferences prefs)
    {
        if (prefs.SubGroupOrder.Count == 0) return 0;
        var aIdx = GroupOrderIndex(a, prefs.SubGroupOrder);
        var bIdx = GroupOrderIndex(b, prefs.SubGroupOrder);
        if (aIdx < 0 && bIdx < 0) return 0;
        if (aIdx < 0) return 1;
        if (bIdx < 0) return -1;
        return aIdx.CompareTo(bIdx);
    }

    /// <summary>
    /// Returns the best (lowest) index in <paramref name="order"/> for a candidate's
    /// group, checking both <see cref="VideoReleaseCandidate.GroupName"/> and
    /// <see cref="VideoReleaseCandidate.GroupShortName"/>. Returns -1 if neither matches.
    /// </summary>
    private static int GroupOrderIndex(VideoReleaseCandidate c, List<string> order)
    {
        var nameIdx = string.IsNullOrEmpty(c.GroupName)
            ? -1
            : order.FindIndex(s => string.Equals(s, c.GroupName, StringComparison.OrdinalIgnoreCase));
        var shortIdx = string.IsNullOrEmpty(c.GroupShortName)
            ? -1
            : order.FindIndex(s => string.Equals(s, c.GroupShortName, StringComparison.OrdinalIgnoreCase));
        return (nameIdx, shortIdx) switch
        {
            (< 0, < 0) => -1,
            (< 0, _)   => shortIdx,
            (_, < 0)   => nameIdx,
            _          => Math.Min(nameIdx, shortIdx),
        };
    }

    private static int CompareCorrupted(VideoReleaseCandidate a, VideoReleaseCandidate b)
    {
        // Not corrupt beats corrupt. Both same → tie.
        if (a.IsCorrupted == b.IsCorrupted) return 0;
        return a.IsCorrupted ? 1 : -1;  // a corrupt → b wins (+1 → b wins, -1 → a wins)
    }

    private static int CompareCensored(VideoReleaseCandidate a, VideoReleaseCandidate b)
    {
        if (a.IsCensored is null || b.IsCensored is null) return 0;
        if (a.IsCensored == b.IsCensored) return 0;
        // Not censored beats censored
        return a.IsCensored == true ? 1 : -1;
    }

    private static int CompareCreditless(VideoReleaseCandidate a, VideoReleaseCandidate b)
    {
        if (a.IsCreditless is null || b.IsCreditless is null) return 0;
        if (a.IsCreditless == b.IsCreditless) return 0;
        // Creditless beats non-creditless
        return a.IsCreditless == true ? -1 : 1;
    }
}
