using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ReleaseComparisonService"/> covering signal comparison,
/// ranking, and redundancy detection.
/// </summary>
public class ReleaseComparisonTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static ReleaseComparisonService MakeService(ReleaseComparisonPreferences? prefs = null)
    {
        prefs ??= new ReleaseComparisonPreferences();
        var settings = new ServerSettings { ReleaseComparisonPreferences = prefs };
        var mock = new Mock<ISettingsProvider>();
        mock.Setup(p => p.GetSettings()).Returns(settings);
        return new ReleaseComparisonService(mock.Object, null!);
    }

    private static VideoReleaseCandidate MakeCandidate(
        string key = "key",
        ReleaseSource source = ReleaseSource.Unknown,
        string? resolution = null,
        string? videoCodec = null,
        int bitDepth = 0,
        int audioStreamCount = 0,
        int subtitleStreamCount = 0,
        string? audioCodec = null,
        bool? isChaptered = null,
        string? groupShortName = null,
        int version = 0,
        bool isCorrupted = false,
        bool? isCensored = null,
        bool isHomogeneous = true,
        IReadOnlySet<(EpisodeType, int)>? coverage = null,
        IReadOnlyList<VideoLocal_Place>? places = null,
        bool hasReleaseInfo = true,
        bool isMixed = false,
        bool isChapteredMixed = false,
        bool isCensoredMixed = false,
        bool isCreditlessMixed = false,
        IReadOnlyDictionary<(EpisodeType, int), string?>? episodeGroupMap = null,
        IReadOnlyDictionary<EpisodeType, EpisodeTypeQualitySignals>? typeSignals = null)
        => new()
        {
            Key = key,
            Source = source,
            Resolution = resolution,
            VideoCodec = videoCodec,
            BitDepth = bitDepth,
            AudioStreamCount = audioStreamCount,
            SubtitleStreamCount = subtitleStreamCount,
            AudioCodec = audioCodec,
            IsChaptered = isChaptered,
            GroupShortName = groupShortName,
            Version = version,
            IsCorrupted = isCorrupted,
            IsCensored = isCensored,
            IsHomogeneous = isHomogeneous,
            EpisodeCoverage = coverage ?? new HashSet<(EpisodeType, int)>(),
            Places = places ?? [],
            HasReleaseInfo = hasReleaseInfo,
            IsMixed = isMixed,
            IsChapteredMixed = isChapteredMixed,
            IsCensoredMixed = isCensoredMixed,
            IsCreditlessMixed = isCreditlessMixed,
            EpisodeGroupMap = episodeGroupMap ?? new Dictionary<(EpisodeType, int), string?>(),
            TypeSignals = typeSignals ?? new Dictionary<EpisodeType, EpisodeTypeQualitySignals>(),
        };

    private static IReadOnlySet<(EpisodeType, int)> Episodes(params (EpisodeType, int)[] eps)
        => new HashSet<(EpisodeType, int)>(eps);

    private static VideoLocal_Place MakePlace(int id) => new() { ID = id };

    // ── signal comparison ─────────────────────────────────────────────────────

    [Fact]
    public void BetterSourceWins_FirstSignalDecides()
    {
        var svc = MakeService();
        var bluray = MakeCandidate(source: ReleaseSource.BluRay);
        var web = MakeCandidate(source: ReleaseSource.Web);

        Assert.True(svc.Compare(bluray, web) < 0, "BluRay should beat Web");
        Assert.True(svc.Compare(web, bluray) > 0, "Web should lose to BluRay");
    }

    [Fact]
    public void TiedSource_FallsToNextSignal_ResolutionDecides()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.Source, ReleaseSignalType.Resolution],
        };
        var svc = MakeService(prefs);

        var hd = MakeCandidate(source: ReleaseSource.BluRay, resolution: "1080p");
        var sd = MakeCandidate(source: ReleaseSource.BluRay, resolution: "720p");

        Assert.True(svc.Compare(hd, sd) < 0, "1080p should beat 720p when source tied");
    }

    [Fact]
    public void NullSignalSkipped_BothNullCodec_Tie()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.VideoCodec],
        };
        var svc = MakeService(prefs);

        var a = MakeCandidate(videoCodec: null);
        var b = MakeCandidate(videoCodec: null);

        Assert.Equal(0, svc.Compare(a, b));
    }

    [Fact]
    public void NullSignalSkipped_OneNullCodec_OtherWins()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.VideoCodec],
        };
        var svc = MakeService(prefs);

        var withCodec = MakeCandidate(videoCodec: "HEVC");
        var withoutCodec = MakeCandidate(videoCodec: null);

        Assert.True(svc.Compare(withCodec, withoutCodec) < 0, "Known codec beats unknown");
    }

    [Fact]
    public void CorruptLosesToClean()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.Corrupted],
        };
        var svc = MakeService(prefs);

        var clean = MakeCandidate(isCorrupted: false);
        var corrupt = MakeCandidate(isCorrupted: true);

        Assert.True(svc.Compare(clean, corrupt) < 0, "Clean should beat corrupt");
        Assert.True(svc.Compare(corrupt, clean) > 0, "Corrupt should lose to clean");
    }

    [Fact]
    public void HigherBitDepthPreferred()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.BitDepth],
            PreferHigherBitDepth = true,
        };
        var svc = MakeService(prefs);

        var tenBit = MakeCandidate(bitDepth: 10);
        var eightBit = MakeCandidate(bitDepth: 8);

        Assert.True(svc.Compare(tenBit, eightBit) < 0, "10-bit should beat 8-bit when PreferHigherBitDepth");
    }

    [Fact]
    public void ChaptersPreferred()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.Chaptered],
        };
        var svc = MakeService(prefs);

        var chaptered = MakeCandidate(isChaptered: true);
        var notChaptered = MakeCandidate(isChaptered: false);

        Assert.True(svc.Compare(chaptered, notChaptered) < 0, "Chaptered beats non-chaptered");
    }

    [Fact]
    public void HomogeneousBeatsHeterogeneousGapFill()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.GroupHomogeneity],
        };
        var svc = MakeService(prefs);

        var sameGroup = MakeCandidate(isHomogeneous: true);
        var mixedGroup = MakeCandidate(isHomogeneous: false);

        Assert.True(svc.Compare(sameGroup, mixedGroup) < 0, "Same-group gap-fill should beat cross-group gap-fill");
    }

    [Fact]
    public void HigherVersionPreferred()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.Version],
        };
        var svc = MakeService(prefs);

        var v2 = MakeCandidate(version: 2);
        var v1 = MakeCandidate(version: 1);

        Assert.True(svc.Compare(v2, v1) < 0, "v2 should beat v1");
    }

    [Fact]
    public void SubGroupOrder_Respected()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.SubGroup],
            SubGroupOrder = ["Group-A", "Group-B"],
        };
        var svc = MakeService(prefs);

        var groupA = MakeCandidate(groupShortName: "Group-A");
        var groupB = MakeCandidate(groupShortName: "Group-B");

        Assert.True(svc.Compare(groupA, groupB) < 0, "Group-A ranked first should beat Group-B");
    }

    // ── ranking ───────────────────────────────────────────────────────────────

    [Fact]
    public void Rank_ReturnsBestFirst()
    {
        var svc = MakeService();
        var bluray = MakeCandidate(key: "br", source: ReleaseSource.BluRay);
        var web = MakeCandidate(key: "web", source: ReleaseSource.Web);
        var dvd = MakeCandidate(key: "dvd", source: ReleaseSource.DVD);

        var ranked = svc.Rank([web, dvd, bluray]);

        Assert.Equal("br", ranked[0].Key);
        Assert.Equal("dvd", ranked[1].Key);
        Assert.Equal("web", ranked[2].Key);
    }

    // ── redundancy detection ──────────────────────────────────────────────────

    [Fact]
    public void SingleCandidate_NeverRedundant()
    {
        var svc = MakeService();
        var only = MakeCandidate(source: ReleaseSource.BluRay, coverage: Episodes((EpisodeType.Episode, 1)));

        var ranked = svc.Rank([only]);
        var redundant = svc.GetRedundantCandidates(ranked[0].EpisodeCoverage, ranked.Skip(1).ToList());

        Assert.Empty(redundant);
    }

    [Fact]
    public void FullCoverage_LowerRanked_IsRedundant()
    {
        // 1080p BluRay covers eps 1-3; 720p covers the same eps → 720p is redundant.
        var svc = MakeService();
        var eps = Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2), (EpisodeType.Episode, 3));

        var hd = MakeCandidate(key: "1080p", source: ReleaseSource.BluRay, resolution: "1080p", coverage: eps);
        var sd = MakeCandidate(key: "720p", source: ReleaseSource.BluRay, resolution: "720p", coverage: eps);

        var ranked = svc.Rank([sd, hd]);
        var redundant = svc.GetRedundantCandidates(ranked[0].EpisodeCoverage, ranked.Skip(1).ToList());

        Assert.Single(redundant);
        Assert.Equal("720p", redundant[0].Key);
    }

    [Fact]
    public void PartialCoverage_HigherRanked_IsNotRedundant()
    {
        // 1080p only has eps 1-4 of a 6-ep series; 720p has eps 1-6.
        // 1080p is better but doesn't yet cover 720p fully → 720p is NOT redundant.
        var svc = MakeService();
        var hdEps = Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2),
                             (EpisodeType.Episode, 3), (EpisodeType.Episode, 4));
        var sdEps = Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2),
                             (EpisodeType.Episode, 3), (EpisodeType.Episode, 4),
                             (EpisodeType.Episode, 5), (EpisodeType.Episode, 6));

        var hd = MakeCandidate(key: "1080p", source: ReleaseSource.BluRay, resolution: "1080p", coverage: hdEps);
        var sd = MakeCandidate(key: "720p", source: ReleaseSource.BluRay, resolution: "720p", coverage: sdEps);

        var ranked = svc.Rank([sd, hd]);
        var redundant = svc.GetRedundantCandidates(ranked[0].EpisodeCoverage, ranked.Skip(1).ToList());

        Assert.Empty(redundant);
    }

    [Fact]
    public void LowerRanked_WithSubsetCoverage_IsRedundant()
    {
        // 720p only has eps 1-4 (a subset), 1080p has 1-6 → 720p is redundant since 1080p covers it.
        var svc = MakeService();
        var hdEps = Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2),
                             (EpisodeType.Episode, 3), (EpisodeType.Episode, 4),
                             (EpisodeType.Episode, 5), (EpisodeType.Episode, 6));
        var sdEps = Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2),
                             (EpisodeType.Episode, 3), (EpisodeType.Episode, 4));

        var hd = MakeCandidate(key: "1080p", source: ReleaseSource.BluRay, resolution: "1080p", coverage: hdEps);
        var sd = MakeCandidate(key: "720p", source: ReleaseSource.BluRay, resolution: "720p", coverage: sdEps);

        var ranked = svc.Rank([sd, hd]);
        var redundant = svc.GetRedundantCandidates(ranked[0].EpisodeCoverage, ranked.Skip(1).ToList());

        Assert.Single(redundant);
        Assert.Equal("720p", redundant[0].Key);
    }

    [Fact]
    public void NoCoverageData_CandidateNotRedundant()
    {
        // When episode coverage is empty (unrecognized files), never declare redundant.
        var svc = MakeService();

        var hd = MakeCandidate(key: "1080p", source: ReleaseSource.BluRay, resolution: "1080p",
            coverage: new HashSet<(EpisodeType, int)>());
        var sd = MakeCandidate(key: "720p", source: ReleaseSource.BluRay, resolution: "720p",
            coverage: new HashSet<(EpisodeType, int)>());

        var ranked = svc.Rank([sd, hd]);
        var redundant = svc.GetRedundantCandidates(ranked[0].EpisodeCoverage, ranked.Skip(1).ToList());

        Assert.Empty(redundant);
    }

    [Fact]
    public void SpecialsAndEpisodes_AreDistinct_NoFalseRedundancy()
    {
        // Special 1 ≠ Episode 1 — different episode types should not cross-count.
        var svc = MakeService();
        var specialEps = Episodes((EpisodeType.Special, 1), (EpisodeType.Special, 2));
        var regularEps = Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2));

        var hd = MakeCandidate(key: "1080p", source: ReleaseSource.BluRay, resolution: "1080p", coverage: regularEps);
        var sd = MakeCandidate(key: "special", source: ReleaseSource.BluRay, resolution: "720p", coverage: specialEps);

        var ranked = svc.Rank([sd, hd]);
        var redundant = svc.GetRedundantCandidates(ranked[0].EpisodeCoverage, ranked.Skip(1).ToList());

        // The 1080p covers (Episode,1),(Episode,2); the special covers (Special,1),(Special,2).
        // They don't overlap → neither is redundant.
        Assert.Empty(redundant);
    }

    // ── CompareWithDecision ───────────────────────────────────────────────────

    [Fact]
    public void CompareWithDecision_ReturnsDecidingSignalAndValues()
    {
        var svc = MakeService();
        var bluray = MakeCandidate(source: ReleaseSource.BluRay);
        var web = MakeCandidate(source: ReleaseSource.Web);

        var decision = svc.CompareWithDecision(bluray, web);

        Assert.True(decision.Result < 0, "BluRay should win");
        Assert.Equal(ReleaseSignalType.Source, decision.DecidingSignal);
        Assert.Equal("BluRay", decision.PrimaryValue);
        Assert.Equal("Web", decision.RunnerUpValue);
    }

    [Fact]
    public void CompareWithDecision_BWins_PrimaryValueIsB()
    {
        // When b ranks higher, PrimaryValue should describe b (the winner), not a.
        var svc = MakeService();
        var web = MakeCandidate(source: ReleaseSource.Web);
        var bluray = MakeCandidate(source: ReleaseSource.BluRay);

        var decision = svc.CompareWithDecision(web, bluray);

        Assert.True(decision.Result > 0, "BluRay (b) should win");
        Assert.Equal("BluRay", decision.PrimaryValue);
        Assert.Equal("Web", decision.RunnerUpValue);
    }

    [Fact]
    public void CompareWithDecision_AllTied_ReturnsNullSignal()
    {
        var prefs = new ReleaseComparisonPreferences
        {
            SignalPriority = [ReleaseSignalType.Source],
        };
        var svc = MakeService(prefs);
        var a = MakeCandidate(source: ReleaseSource.BluRay);
        var b = MakeCandidate(source: ReleaseSource.BluRay);

        var decision = svc.CompareWithDecision(a, b);

        Assert.Equal(0, decision.Result);
        Assert.Null(decision.DecidingSignal);
        Assert.Null(decision.PrimaryValue);
        Assert.Null(decision.RunnerUpValue);
    }

    // ── per-file redundancy ───────────────────────────────────────────────────

    [Fact]
    public void GetRedundantPlaces_FileCoveredByPrimary_IsRedundant()
    {
        var svc = MakeService();
        var primary = MakeCandidate(key: "1080p", resolution: "1080p",
            coverage: Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2), (EpisodeType.Episode, 3)));

        var place = MakePlace(1);
        var secondary = MakeCandidate(key: "720p", resolution: "720p",
            coverage: Episodes((EpisodeType.Episode, 1)),
            places: [place]);

        var redundant = svc.GetRedundantPlaces(primary.EpisodeCoverage, secondary.Places,
            _ => Episodes((EpisodeType.Episode, 1)));

        Assert.Single(redundant);
        Assert.Same(place, redundant[0]);
    }

    [Fact]
    public void GetRedundantPlaces_FileCoversEpisodeNotInPrimary_IsRetained()
    {
        var svc = MakeService();
        var primary = MakeCandidate(key: "1080p", resolution: "1080p",
            coverage: Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2)));

        var place = MakePlace(3);
        var secondary = MakeCandidate(key: "720p", resolution: "720p",
            coverage: Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2), (EpisodeType.Episode, 3)),
            places: [place]);

        var redundant = svc.GetRedundantPlaces(primary.EpisodeCoverage, secondary.Places,
            _ => Episodes((EpisodeType.Episode, 3)));

        Assert.Empty(redundant);
    }

    [Fact]
    public void GetRedundantPlaces_EmptyFileCoverage_AlwaysRetained()
    {
        // File with no SRI (empty coverage) must never be considered redundant.
        var svc = MakeService();
        var primary = MakeCandidate(key: "1080p", resolution: "1080p",
            coverage: Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2)));

        var place = MakePlace(1);
        var secondary = MakeCandidate(key: "720p", resolution: "720p",
            coverage: new HashSet<(EpisodeType, int)>(),
            places: [place]);

        var redundant = svc.GetRedundantPlaces(primary.EpisodeCoverage, secondary.Places,
            _ => new HashSet<(EpisodeType, int)>());

        Assert.Empty(redundant);
    }

    [Fact]
    public void GetRedundantPlaces_PrimaryHasNoCoverage_NothingRedundant()
    {
        // When the primary has no coverage data we can't safely judge redundancy.
        var svc = MakeService();
        var primary = MakeCandidate(key: "1080p", resolution: "1080p",
            coverage: new HashSet<(EpisodeType, int)>());

        var place = MakePlace(1);
        var secondary = MakeCandidate(key: "720p", resolution: "720p",
            coverage: Episodes((EpisodeType.Episode, 1)),
            places: [place]);

        var redundant = svc.GetRedundantPlaces(primary.EpisodeCoverage, secondary.Places,
            _ => Episodes((EpisodeType.Episode, 1)));

        Assert.Empty(redundant);
    }

    [Fact]
    public void GetRedundantPlaces_AiringSeriesScenario_OnlyUncoveredEpisodeKept()
    {
        // Primary (HEVC) covers eps 1-2. Secondary (H264) has files for eps 1, 2, 3.
        // Files for 1 and 2 are redundant; file for ep 3 is retained.
        var svc = MakeService();
        var primary = MakeCandidate(key: "hevc", videoCodec: "HEVC",
            coverage: Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2)));

        var placeEp1 = MakePlace(1);
        var placeEp2 = MakePlace(2);
        var placeEp3 = MakePlace(3);
        var secondary = MakeCandidate(key: "h264", videoCodec: "H264",
            coverage: Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2), (EpisodeType.Episode, 3)),
            places: [placeEp1, placeEp2, placeEp3]);

        var coverageByPlace = new Dictionary<VideoLocal_Place, IReadOnlySet<(EpisodeType, int)>>
        {
            [placeEp1] = Episodes((EpisodeType.Episode, 1)),
            [placeEp2] = Episodes((EpisodeType.Episode, 2)),
            [placeEp3] = Episodes((EpisodeType.Episode, 3)),
        };

        var redundant = svc.GetRedundantPlaces(primary.EpisodeCoverage, secondary.Places, p => coverageByPlace[p]);

        Assert.Equal(2, redundant.Count);
        Assert.Contains(placeEp1, redundant);
        Assert.Contains(placeEp2, redundant);
        Assert.DoesNotContain(placeEp3, redundant);
    }

    // ── primary eligibility gate ──────────────────────────────────────────────

    [Fact]
    public void GetEligiblePrimaryCoverage_CleanSingleRelease_ReturnsFullCoverage()
    {
        var svc = MakeService();
        var eps = Episodes((EpisodeType.Episode, 1), (EpisodeType.Episode, 2));
        var primary = MakeCandidate(coverage: eps);

        var eligible = svc.GetEligiblePrimaryCoverage(primary);

        Assert.Equal(eps, eligible);
    }

    [Fact]
    public void GetEligiblePrimaryCoverage_Mixed_ReturnsEmpty()
    {
        var svc = MakeService();
        var primary = MakeCandidate(
            coverage: Episodes((EpisodeType.Episode, 1)),
            isMixed: true);

        Assert.Empty(svc.GetEligiblePrimaryCoverage(primary));
    }

    [Fact]
    public void GetEligiblePrimaryCoverage_MissingReleaseInfo_ReturnsEmpty()
    {
        var svc = MakeService();
        var primary = MakeCandidate(
            coverage: Episodes((EpisodeType.Episode, 1)),
            hasReleaseInfo: false);

        Assert.Empty(svc.GetEligiblePrimaryCoverage(primary));
    }

    [Fact]
    public void GetEligiblePrimaryCoverage_Corrupted_ReturnsEmpty()
    {
        var svc = MakeService();
        var primary = MakeCandidate(
            coverage: Episodes((EpisodeType.Episode, 1)),
            isCorrupted: true);

        Assert.Empty(svc.GetEligiblePrimaryCoverage(primary));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void GetEligiblePrimaryCoverage_InternalDisagreement_ReturnsEmpty(
        bool chapteredMixed, bool censoredMixed, bool creditlessMixed)
    {
        var svc = MakeService();
        var primary = MakeCandidate(
            coverage: Episodes((EpisodeType.Episode, 1)),
            isChapteredMixed: chapteredMixed,
            isCensoredMixed: censoredMixed,
            isCreditlessMixed: creditlessMixed);

        Assert.Empty(svc.GetEligiblePrimaryCoverage(primary));
    }

    [Fact]
    public void GetEligiblePrimaryCoverage_BestPerType_EligibleForOneTypeOnly()
    {
        // Regular episodes are a clean single release; specials are mixed (two
        // contributing groups) — under BestPerType, eligibility should be granted
        // for Episode but denied for Special.
        var prefs = new ReleaseComparisonPreferences { EpisodeTypeScope = EpisodeTypeScope.BestPerType };
        var svc = MakeService(prefs);

        var episodeGroupMap = new Dictionary<(EpisodeType, int), string?>
        {
            [(EpisodeType.Episode, 1)] = "GroupA",
            [(EpisodeType.Episode, 2)] = "GroupA",
            [(EpisodeType.Special, 1)] = "GroupA",
            [(EpisodeType.Special, 2)] = "GroupB",
        };

        var primary = MakeCandidate(
            coverage: Episodes(
                (EpisodeType.Episode, 1), (EpisodeType.Episode, 2),
                (EpisodeType.Special, 1), (EpisodeType.Special, 2)),
            isMixed: true, // whole-candidate mixed (spans groups across types) — irrelevant under BestPerType
            episodeGroupMap: episodeGroupMap);

        var eligible = svc.GetEligiblePrimaryCoverage(primary);

        Assert.Equal(2, eligible.Count);
        Assert.Contains((EpisodeType.Episode, 1), eligible);
        Assert.Contains((EpisodeType.Episode, 2), eligible);
        Assert.DoesNotContain((EpisodeType.Special, 1), eligible);
        Assert.DoesNotContain((EpisodeType.Special, 2), eligible);
    }

    [Fact]
    public void GetEligiblePrimaryCoverage_BestPerType_CorruptedTypeExcluded()
    {
        var prefs = new ReleaseComparisonPreferences { EpisodeTypeScope = EpisodeTypeScope.BestPerType };
        var svc = MakeService(prefs);

        var episodeGroupMap = new Dictionary<(EpisodeType, int), string?>
        {
            [(EpisodeType.Episode, 1)] = "GroupA",
            [(EpisodeType.Special, 1)] = "GroupA",
        };
        var typeSignals = new Dictionary<EpisodeType, EpisodeTypeQualitySignals>
        {
            [EpisodeType.Episode] = new(ReleaseSource.Web, null, null, 0, null, 0, 0, null, null, null, false, [], []),
            [EpisodeType.Special] = new(ReleaseSource.Web, null, null, 0, null, 0, 0, null, null, null, true, [], []),
        };

        var primary = MakeCandidate(
            coverage: Episodes((EpisodeType.Episode, 1), (EpisodeType.Special, 1)),
            episodeGroupMap: episodeGroupMap,
            typeSignals: typeSignals);

        var eligible = svc.GetEligiblePrimaryCoverage(primary);

        Assert.Single(eligible);
        Assert.Contains((EpisodeType.Episode, 1), eligible);
    }

    [Fact]
    public void GetRedundantCandidates_UsesEligiblePrimaryCoverage_NotRawCoverage()
    {
        // A mixed primary is ranked first but is not eligible to trump — passing its
        // raw EpisodeCoverage would incorrectly mark the secondary redundant; passing
        // GetEligiblePrimaryCoverage's (empty) result correctly retains it.
        var svc = MakeService();
        var eps = Episodes((EpisodeType.Episode, 1));
        var primary = MakeCandidate(key: "mixed-primary", coverage: eps, isMixed: true);
        var secondary = MakeCandidate(key: "secondary", coverage: eps);

        var eligibleCoverage = svc.GetEligiblePrimaryCoverage(primary);
        var redundant = svc.GetRedundantCandidates(eligibleCoverage, [secondary]);

        Assert.Empty(redundant);
    }

    [Fact]
    public void GetRedundantCandidates_BypassingEligibilityGate_UsesRawCoverage()
    {
        // Mirrors an explicit per-series "PreferredCandidateKey" override: the caller
        // deliberately chose this mixed primary, so it passes the primary's raw
        // EpisodeCoverage directly instead of GetEligiblePrimaryCoverage's (empty)
        // result — the secondary is then correctly found redundant.
        var svc = MakeService();
        var eps = Episodes((EpisodeType.Episode, 1));
        var primary = MakeCandidate(key: "mixed-primary", coverage: eps, isMixed: true);
        var secondary = MakeCandidate(key: "secondary", coverage: eps);

        var redundant = svc.GetRedundantCandidates(primary.EpisodeCoverage, [secondary]);

        Assert.Single(redundant);
        Assert.Equal("secondary", redundant[0].Key);
    }
}
