using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

public class TmdbLinkingServiceTests
{
    // Reproduces "Heroine? Saint? No, I'm an All-Works Maid (And Proud of It)!": AniDB tracks an
    // early exclusive-stream premiere for episode 1 (2026-06-24) that TMDB never listed. TMDB's S1E1
    // is dated 2026-07-01, which happens to be AniDB episode 2's air date, so a date-only match grabs
    // TMDB S1E1 for AniDB episode 2 before episode 1 gets a turn, leaving episode 1 to fall back to
    // "first available" (TMDB S1E2). Reconciliation should swap them back into AniDB order.
    [Fact]
    public void ReconcileEpisodeOrderInversions_SwapsBackToAnidbOrder_WhenBothMatchesAreWeak()
    {
        var anidbEp1 = new AniDB_Episode { EpisodeID = 1, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode };
        var anidbEp2 = new AniDB_Episode { EpisodeID = 2, EpisodeNumber = 2, EpisodeType = EpisodeType.Episode };
        var anidbEpisodes = new Dictionary<int, AniDB_Episode> { [1] = anidbEp1, [2] = anidbEp2 };

        var tmdbEp1 = new TMDB_Episode { TmdbEpisodeID = 101, SeasonNumber = 1, EpisodeNumber = 1 };
        var tmdbEp2 = new TMDB_Episode { TmdbEpisodeID = 102, SeasonNumber = 1, EpisodeNumber = 2 };
        var tmdbEpisodeDict = new Dictionary<int, TMDB_Episode> { [101] = tmdbEp1, [102] = tmdbEp2 };

        // Bug reproduction: episode 1 fell back to "first available" TMDB S1E2, episode 2 grabbed
        // TMDB S1E1 via a coincidental exact air-date match.
        var xrefEp1 = new CrossRef_AniDB_TMDB_Episode(anidbEp1.EpisodeID, 0, tmdbEp2.TmdbEpisodeID, 0, MatchRating.FirstAvailable);
        var xrefEp2 = new CrossRef_AniDB_TMDB_Episode(anidbEp2.EpisodeID, 0, tmdbEp1.TmdbEpisodeID, 0, MatchRating.DateMatches);
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode> { xrefEp1, xrefEp2 };

        TmdbLinkingService.ReconcileEpisodeOrderInversions(anidbEpisodes, tmdbEpisodeDict, toAdd);

        Assert.Equal(tmdbEp1.TmdbEpisodeID, xrefEp1.TmdbEpisodeID);
        Assert.Equal(tmdbEp2.TmdbEpisodeID, xrefEp2.TmdbEpisodeID);
        // MatchRating describes the evidence for the pairing, so it must travel with the swap too.
        Assert.Equal(MatchRating.DateMatches, xrefEp1.MatchRating);
        Assert.Equal(MatchRating.FirstAvailable, xrefEp2.MatchRating);
    }

    [Fact]
    public void ReconcileEpisodeOrderInversions_LeavesOrderAlone_WhenAlreadyCorrect()
    {
        var anidbEp1 = new AniDB_Episode { EpisodeID = 1, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode };
        var anidbEp2 = new AniDB_Episode { EpisodeID = 2, EpisodeNumber = 2, EpisodeType = EpisodeType.Episode };
        var anidbEpisodes = new Dictionary<int, AniDB_Episode> { [1] = anidbEp1, [2] = anidbEp2 };

        var tmdbEp1 = new TMDB_Episode { TmdbEpisodeID = 101, SeasonNumber = 1, EpisodeNumber = 1 };
        var tmdbEp2 = new TMDB_Episode { TmdbEpisodeID = 102, SeasonNumber = 1, EpisodeNumber = 2 };
        var tmdbEpisodeDict = new Dictionary<int, TMDB_Episode> { [101] = tmdbEp1, [102] = tmdbEp2 };

        var xrefEp1 = new CrossRef_AniDB_TMDB_Episode(anidbEp1.EpisodeID, 0, tmdbEp1.TmdbEpisodeID, 0, MatchRating.FirstAvailable);
        var xrefEp2 = new CrossRef_AniDB_TMDB_Episode(anidbEp2.EpisodeID, 0, tmdbEp2.TmdbEpisodeID, 0, MatchRating.DateMatches);
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode> { xrefEp1, xrefEp2 };

        TmdbLinkingService.ReconcileEpisodeOrderInversions(anidbEpisodes, tmdbEpisodeDict, toAdd);

        Assert.Equal(tmdbEp1.TmdbEpisodeID, xrefEp1.TmdbEpisodeID);
        Assert.Equal(tmdbEp2.TmdbEpisodeID, xrefEp2.TmdbEpisodeID);
    }

    // A strong (title-corroborated) match should never be reshuffled just because its weak neighbor
    // looks out of order — the title evidence is trusted over positional guessing.
    [Fact]
    public void ReconcileEpisodeOrderInversions_DoesNotTouchStrongMatches()
    {
        var anidbEp1 = new AniDB_Episode { EpisodeID = 1, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode };
        var anidbEp2 = new AniDB_Episode { EpisodeID = 2, EpisodeNumber = 2, EpisodeType = EpisodeType.Episode };
        var anidbEpisodes = new Dictionary<int, AniDB_Episode> { [1] = anidbEp1, [2] = anidbEp2 };

        var tmdbEp1 = new TMDB_Episode { TmdbEpisodeID = 101, SeasonNumber = 1, EpisodeNumber = 1 };
        var tmdbEp2 = new TMDB_Episode { TmdbEpisodeID = 102, SeasonNumber = 1, EpisodeNumber = 2 };
        var tmdbEpisodeDict = new Dictionary<int, TMDB_Episode> { [101] = tmdbEp1, [102] = tmdbEp2 };

        // Episode 1 has a confirmed title match pointing at S1E2 (e.g. a legitimately reordered
        // episode); episode 2 only has a weak positional guess at S1E1. This should not be swapped.
        var xrefEp1 = new CrossRef_AniDB_TMDB_Episode(anidbEp1.EpisodeID, 0, tmdbEp2.TmdbEpisodeID, 0, MatchRating.TitleMatches);
        var xrefEp2 = new CrossRef_AniDB_TMDB_Episode(anidbEp2.EpisodeID, 0, tmdbEp1.TmdbEpisodeID, 0, MatchRating.FirstAvailable);
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode> { xrefEp1, xrefEp2 };

        TmdbLinkingService.ReconcileEpisodeOrderInversions(anidbEpisodes, tmdbEpisodeDict, toAdd);

        Assert.Equal(tmdbEp2.TmdbEpisodeID, xrefEp1.TmdbEpisodeID);
        Assert.Equal(tmdbEp1.TmdbEpisodeID, xrefEp2.TmdbEpisodeID);
    }

    // A single left-to-right adjacent-swap pass only bubbles one inversion per pass — a full
    // 3-episode reversal (AniDB 1,2,3 -> TMDB 3,2,1) needs two passes to fully sort. Confirms the
    // reconciliation loops until stable rather than stopping after one pass.
    [Fact]
    public void ReconcileEpisodeOrderInversions_FullyUntangles_ThreeEpisodeReversal()
    {
        var anidbEpisodes = new Dictionary<int, AniDB_Episode>
        {
            [1] = new() { EpisodeID = 1, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode },
            [2] = new() { EpisodeID = 2, EpisodeNumber = 2, EpisodeType = EpisodeType.Episode },
            [3] = new() { EpisodeID = 3, EpisodeNumber = 3, EpisodeType = EpisodeType.Episode },
        };

        var tmdbEpisodeDict = new Dictionary<int, TMDB_Episode>
        {
            [101] = new() { TmdbEpisodeID = 101, SeasonNumber = 1, EpisodeNumber = 1 },
            [102] = new() { TmdbEpisodeID = 102, SeasonNumber = 1, EpisodeNumber = 2 },
            [103] = new() { TmdbEpisodeID = 103, SeasonNumber = 1, EpisodeNumber = 3 },
        };

        var xrefEp1 = new CrossRef_AniDB_TMDB_Episode(1, 0, 103, 0, MatchRating.FirstAvailable);
        var xrefEp2 = new CrossRef_AniDB_TMDB_Episode(2, 0, 102, 0, MatchRating.DateMatches);
        var xrefEp3 = new CrossRef_AniDB_TMDB_Episode(3, 0, 101, 0, MatchRating.FirstAvailable);
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode> { xrefEp1, xrefEp2, xrefEp3 };

        TmdbLinkingService.ReconcileEpisodeOrderInversions(anidbEpisodes, tmdbEpisodeDict, toAdd);

        Assert.Equal(101, xrefEp1.TmdbEpisodeID);
        Assert.Equal(102, xrefEp2.TmdbEpisodeID);
        Assert.Equal(103, xrefEp3.TmdbEpisodeID);
    }

    // Specials and normal episodes are matched independently and should never be reconciled
    // against each other even if their AniDB episode numbers happen to collide (e.g. Special 1
    // and Episode 1).
    [Fact]
    public void ReconcileEpisodeOrderInversions_DoesNotCrossEpisodeTypeBoundary()
    {
        var anidbEpisodes = new Dictionary<int, AniDB_Episode>
        {
            [1] = new() { EpisodeID = 1, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode },
            [2] = new() { EpisodeID = 2, EpisodeNumber = 1, EpisodeType = EpisodeType.Special },
        };

        var tmdbEpisodeDict = new Dictionary<int, TMDB_Episode>
        {
            [101] = new() { TmdbEpisodeID = 101, SeasonNumber = 1, EpisodeNumber = 2 },
            [102] = new() { TmdbEpisodeID = 102, SeasonNumber = 0, EpisodeNumber = 1 },
        };

        var xrefEpisode = new CrossRef_AniDB_TMDB_Episode(1, 0, 101, 0, MatchRating.FirstAvailable);
        var xrefSpecial = new CrossRef_AniDB_TMDB_Episode(2, 0, 102, 0, MatchRating.FirstAvailable);
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode> { xrefEpisode, xrefSpecial };

        TmdbLinkingService.ReconcileEpisodeOrderInversions(anidbEpisodes, tmdbEpisodeDict, toAdd);

        Assert.Equal(101, xrefEpisode.TmdbEpisodeID);
        Assert.Equal(102, xrefSpecial.TmdbEpisodeID);
    }

    // GetAllTitles() normally hits RepoFactory; seeding the private cache field directly keeps this
    // a pure unit test instead of requiring a repository/DB fixture.
    private static void SeedTitles(TMDB_Episode episode, params TMDB_Title[] titles)
    {
        var field = typeof(TMDB_Episode).GetField("_allTitles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(episode, titles);
    }

    private static IReadOnlyList<string> GetEpisodeTitleCandidates(TMDB_Episode episode, string originalLanguageCode)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var method = typeof(TmdbLinkingService).GetMethod("GetEpisodeTitleCandidates", flags)!;
        return (IReadOnlyList<string>)method.Invoke(null, [episode, originalLanguageCode])!;
    }

    // Reproduces the non-English matching feature: a Japanese-original show's episode should be
    // searchable by its en-US or ja title, but not by unrelated-language titles TMDB also stores.
    [Fact]
    public void GetEpisodeTitleCandidates_IncludesEnglishAndOriginalLanguage_ExcludesOthers()
    {
        var episode = new TMDB_Episode { TmdbEpisodeID = 1, EpisodeNumber = 3 };
        SeedTitles(episode,
            new(DataEntityType.Episode, 1, "The Lost Village", "en", "US"),
            new(DataEntityType.Episode, 1, "失われた村", "ja", ""),
            new(DataEntityType.Episode, 1, "失去的村子", "zh", "CN"));

        var candidates = GetEpisodeTitleCandidates(episode, "ja");

        Assert.Contains("The Lost Village", candidates);
        Assert.Contains("失われた村", candidates);
        Assert.DoesNotContain("失去的村子", candidates);
    }

    // TMDB stores the "Episode N" placeholder per-language (not just in English), so it must be
    // excluded from every language's candidates, not just en-US.
    [Fact]
    public void GetEpisodeTitleCandidates_ExcludesPlaceholderInEveryLanguage()
    {
        var episode = new TMDB_Episode { TmdbEpisodeID = 2, EpisodeNumber = 3 };
        SeedTitles(episode,
            new(DataEntityType.Episode, 2, "Episode 3", "en", "US"),
            new(DataEntityType.Episode, 2, "Episode 3", "ja", ""));

        var candidates = GetEpisodeTitleCandidates(episode, "ja");

        Assert.Empty(candidates);
    }

    private static bool TryNearestAirDateMatch(AniDB_Episode anidbEpisode, List<(TMDB_Episode episode, int distance)> nearestAirdate, out CrossRef_AniDB_TMDB_Episode crossRef, out double confidence)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var method = typeof(TmdbLinkingService).GetMethod("TryNearestAirDateMatch", flags)!;
        var args = new object?[] { anidbEpisode, nearestAirdate, null, 0d };
        var result = (bool)method.Invoke(null, args)!;
        crossRef = (CrossRef_AniDB_TMDB_Episode)args[2]!;
        confidence = (double)args[3]!;
        return result;
    }

    // Reproduces "The Elusive Samurai" S2: AniDB episode 2 has no TMDB entry within ±2 days (TMDB
    // simply hasn't listed it yet), so the strict air-date pass finds nothing. Previously this fell
    // straight to a blind positional "first available" guess (grabbing whatever TMDB episode was next
    // in the list, e.g. episode 5's slot); the nearest-date fallback should instead pick the episode
    // whose air date is actually closest, even though it's outside the strict window.
    [Fact]
    public void TryNearestAirDateMatch_PicksClosestCandidate_OutsideStrictWindow()
    {
        var anidbEpisode = new AniDB_Episode { EpisodeID = 2, EpisodeNumber = 2, EpisodeType = EpisodeType.Episode };
        var farEpisode = new TMDB_Episode { TmdbEpisodeID = 201, TmdbShowID = 1, SeasonNumber = 2, EpisodeNumber = 2 };
        var nearEpisode = new TMDB_Episode { TmdbEpisodeID = 202, TmdbShowID = 1, SeasonNumber = 2, EpisodeNumber = 3 };
        var nearestAirdate = new List<(TMDB_Episode episode, int distance)> { (nearEpisode, 5), (farEpisode, 20) };

        var found = TryNearestAirDateMatch(anidbEpisode, nearestAirdate, out var crossRef, out var confidence);

        Assert.True(found);
        Assert.Equal(nearEpisode.TmdbEpisodeID, crossRef.TmdbEpisodeID);
        Assert.Equal(MatchRating.NearestDateMatches, crossRef.MatchRating);
        Assert.True(confidence > 0);
    }

    [Fact]
    public void TryNearestAirDateMatch_ReturnsFalse_WhenNoCandidates()
    {
        var anidbEpisode = new AniDB_Episode { EpisodeID = 2, EpisodeNumber = 2, EpisodeType = EpisodeType.Episode };

        var found = TryNearestAirDateMatch(anidbEpisode, [], out _, out var confidence);

        Assert.False(found);
        Assert.Equal(0, confidence);
    }
}
