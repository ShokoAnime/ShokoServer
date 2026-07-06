using System.Collections.Generic;
using Shoko.Server.Filters;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Utilities;
using Xunit;

// ReSharper disable StringLiteralTypo

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Tests for <see cref="FuzzySearchService.FuzzyScoreAnyName"/> and
/// <see cref="SeriesSearch.NormalizeForIndex"/> behaviour relevant to TMDB auto-match scoring
/// for both shows and movies.
///</summary>
public class TmdbSearchServiceTests
{
    private static readonly FuzzySearchService Service = new();

    // ── FuzzyScoreAnyName: isNotExact gate ─────────────────────────────────
    // The scoring pass uses FuzzyScoreAnyName and only accepts results where
    // isNotExact==true (genuine edit-distance hit). Substring hits (isNotExact==false)
    // are excluded so spinoffs don't score TitleKindaMatches via prefix containment.

    [Fact]
    public void FuzzyScoreAnyName_SubstringHit_IsNotExactFalse()
    {
        // "Tensura" is a substring of "Tensura Nikki" — isNotExact should be false,
        // meaning the scoring path will NOT count this as fuzzyTitle=true.
        var names = new HashSet<string> { "Tensura Nikki: Tensei shitara Slime Datta Ken" };
        var score = Service.FuzzyScoreAnyName("Tensura", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    [Fact]
    public void FuzzyScoreAnyName_EditDistanceHit_IsNotExactTrue()
    {
        // "Kimetzu no Yaiba" is 1 edit from "Kimetsu no Yaiba" — isNotExact should be true,
        // meaning the scoring path WILL count this as fuzzyTitle=true.
        var names = new HashSet<string> { "Kimetsu no Yaiba" };
        var score = Service.FuzzyScoreAnyName("Kimetzu no Yaiba", names);
        Assert.NotNull(score);
        Assert.True(score!.Value.isNotExact);
    }

    [Fact]
    public void FuzzyScoreAnyName_OnePunchManSequel_IsNotExactFalse()
    {
        var names = new HashSet<string> { "One Punch Man 2nd Season" };
        var score = Service.FuzzyScoreAnyName("One Punch Man", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    // ── FuzzyScoreAnyName: Blood+ edge case ────────────────────────────────

    [Fact]
    public void FuzzyScoreAnyName_BloodPlus_SubstringHitAgainstBloodC()
    {
        // MinEditDistInText is a substring distance: "blood+" matches the prefix "blood " of
        // "blood c" at 1 edit (+ → space), within budget (GetMaxErrors(6) = 1).
        // Blood-C therefore scores TitleKindaMatches when the query is "Blood+".
        // This is acceptable because the correct Blood+ TMDB entry scores TitleMatches
        // via exact match — SortPriority(TitleMatches)=3 beats TitleKindaMatches=5.
        var names = new HashSet<string> { "Blood-C" };
        var score = Service.FuzzyScoreAnyName("Blood+", names);
        Assert.NotNull(score);
        Assert.True(score!.Value.isNotExact); // edit-distance hit, not substring
    }

    // ── FuzzyScoreAnyName: Re:Zero abbreviated title ────────────────────────

    [Fact]
    public void FuzzyScoreAnyName_ReZero_ShortTitleSubstringHitOnly()
    {
        // "re zero" is a prefix of the long form → substring hit (isNotExact:false).
        // The edit-distance path does not fire; PrefixMatchesAnyName is what saves the match.
        var names = new HashSet<string> { "Re:Zero Starting Life in Another World" };
        var score = Service.FuzzyScoreAnyName("Re:Zero", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    // ── FuzzyScoreAnyName: single-character query gap (known limitation) ────

    [Fact]
    public void FuzzyScoreAnyName_SingleCharTitle_OnlySubstringHit()
    {
        // "C" is a substring of the TMDB title → isNotExact:false.
        // The scoring path excludes these (it gates on isNotExact:true),
        // so the match does not contribute to fuzzyTitle.
        var names = new HashSet<string> { "[C] - The Money of Soul and Possibility Control" };
        var score = Service.FuzzyScoreAnyName("C", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    // ── NormalizeForIndex: wave dash U+301C ─────────────────────────────────

    [Theory]
    [InlineData("〜", "")]                                        // solo wave dash → space, collapses to empty after trim
    [InlineData("Monogatari〜Series", "monogatari series")]       // wave dash as separator
    [InlineData("〜Series〜", "series")]                          // leading/trailing wave dash stripped by whitespace collapse
    public void NormalizeForIndex_WaveDash_MapsToSpace(string input, string expected)
        => Assert.Equal(expected, SeriesSearch.NormalizeForIndex(input).Trim());

    // ── NormalizeForIndex: fullwidth tilde U+FF5E ────────────────────────────

    [Fact]
    public void NormalizeForIndex_FullwidthTilde_MappedToSpace()
    {
        // U+FF5E ～ is decomposed by NFKD to ASCII tilde ~ before the separator mapping runs,
        // so it now gets the same space treatment as ASCII '~' and U+301C 〜.
        var result = SeriesSearch.NormalizeForIndex("Foo～Bar");
        Assert.Equal("foo bar", result);
    }

    // ── NormalizeForIndex: superscript decomposition ─────────────────────────

    [Fact]
    public void NormalizeForIndex_SuperscriptThree_DecomposesToDigit()
    {
        // U+00B3 ³ decomposes to '3' under NFKD — "C³" → "c3".
        Assert.Equal("c3", SeriesSearch.NormalizeForIndex("C³"));
    }

    // ── NormalizeForIndex: exclamation-mark collision (known limitation) ─────
    // '!' is in the punctuation-to-space map, so K-On! and K-On!! both normalize
    // to "k on". Disambiguation must fall back to air date and episode count.

    [Fact]
    public void NormalizeForIndex_KOn_SingleAndDoubleExclamationCollide()
    {
        Assert.Equal(SeriesSearch.NormalizeForIndex("K-On!"), SeriesSearch.NormalizeForIndex("K-On!!"));
    }

    [Fact]
    public void NormalizeForIndex_Working_AllExclamationVariantsCollide()
    {
        var s2 = SeriesSearch.NormalizeForIndex("Working!!");
        var s3 = SeriesSearch.NormalizeForIndex("Working!!!");
        Assert.Equal(s2, s3);
    }

    // ── NormalizeForIndex: subtitle-stripped colon split ─────────────────────
    // The show scorer strips the subtitle at the first colon for non-Japanese titles
    // (e.g. "Fairy Tail: 100 Years Quest" → "Fairy Tail"). This stripped form is
    // fuzzy-eligible only — it cannot produce ExactMatch — so a parent show that
    // matches via its short name scores TitleKindaMatches at most, while a specific
    // TMDB entry whose title exact-matches the full title scores TitleMatches and wins.
    // The prequel-traversal layer also tries the current anime's own main title when
    // the root-series search is tried, and prefers whichever scores better.

    [Fact]
    public void NormalizeForIndex_SubtitleStrippedAtColon_ParentTitleIsolated()
    {
        // "Fairy Tail: 100 Years Quest" strips to "Fairy Tail" at the colon.
        // The stripped form normalizes the same as the parent show title, confirming
        // PrefixMatchesAnyName would fire — but since it's fuzzy-only it cannot
        // produce ExactMatch and the parent scores at most TitleKindaMatches.
        const string fullTitle = "Fairy Tail: 100 Years Quest";
        var colonIndex = fullTitle.IndexOf(':');
        var strippedFromFull = SeriesSearch.NormalizeForIndex(fullTitle[..colonIndex].TrimEnd());
        var parentTitle = SeriesSearch.NormalizeForIndex("Fairy Tail");
        Assert.Equal(parentTitle, strippedFromFull);

        // The full title normalizes differently, so the specific TMDB entry that
        // exact-matches the full title can score TitleMatches and win.
        var normalizedFullTitle = SeriesSearch.NormalizeForIndex("Fairy Tail: 100 Years Quest");
        Assert.NotEqual(strippedFromFull, normalizedFullTitle);
    }

    // ── IsShortFormByEpisodeCount: OVA/Web movie routing threshold ───────────
    // OVA and Web anime with ≤4 main episodes route to movie search first,
    // because TMDB may model them as standalone films even though AniDB treats
    // them as a series (e.g. "Grudge of Edinburgh" = 2 AniDB episodes → 2 TMDB movies).

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]   // two-part film like Grudge of Edinburgh
    [InlineData(4, true)]   // upper bound of short-form heuristic
    [InlineData(5, false)]  // five-episode OVA stays on show search path
    [InlineData(13, false)]
    public void IsShortFormByEpisodeCount_Threshold(int count, bool expected)
        => Assert.Equal(expected, TmdbSearchService.IsShortFormByEpisodeCount(count));

    // ── NormalizeForIndex: exclamation collision in movie context ─────────────
    // Movies lack an episode-count tiebreaker, so when two candidates collide on
    // normalized title the scorer falls back to the first result in rating order.
    // The correct movie should score DateAndTitleMatches over the OVA/extra
    // carrying the same base title, but this test documents the collision so any
    // future change to punctuation handling is intentional.

    [Fact]
    public void NormalizeForIndex_MovieExclamationCollision_TitlesTieOnNormalized()
    {
        // e.g. "Precure All Stars Movie: Haru no Carnival♪" and a variant with
        // different punctuation would both normalize the same way.
        // More concretely: a main movie and a bonus short sharing a base title
        // that differs only in trailing punctuation score identically — the date
        // match is the lever to break the tie.
        var mainMovie = SeriesSearch.NormalizeForIndex("Fairy Tail: Phoenix Priestess");
        var bonusShort = SeriesSearch.NormalizeForIndex("Fairy Tail: Phoenix Priestess!");
        Assert.Equal(mainMovie, bonusShort);
    }
}
