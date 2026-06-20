using Shoko.Server.Providers.TMDB;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

public class TmdbNormalizeTitleTests
{
    [Fact]
    public void PlainAscii_Unchanged()
        => Assert.Equal("ONE PUNCH MAN", TmdbSearchService.NormalizeTitle("ONE PUNCH MAN"));

    [Fact]
    public void FullWidthExclamation_ConvertedToHalfWidth()
        => Assert.Equal("Re:Zero!", TmdbSearchService.NormalizeTitle("Re:Zero！"));

    [Fact]
    public void WaveDash_ReplacedWithSpace()
        => Assert.Equal("A B", TmdbSearchService.NormalizeTitle("A〜B"));

    [Fact]
    public void IdeographicSpace_ReplacedWithSpace()
        => Assert.Equal("A B", TmdbSearchService.NormalizeTitle("A　B"));

    [Fact]
    public void MultipleSpaces_Collapsed()
        => Assert.Equal("A B C", TmdbSearchService.NormalizeTitle("A   B  C"));

    [Fact]
    public void LeadingAndTrailingSpaces_Trimmed()
        => Assert.Equal("Tensura", TmdbSearchService.NormalizeTitle("  Tensura  "));

    [Fact]
    public void MixedFullWidthAndWaveDash_BothNormalized()
        => Assert.Equal("Sword Art Online!", TmdbSearchService.NormalizeTitle("Ｓｗｏｒｄ　Ａｒｔ　Ｏｎｌｉｎｅ！"));
}

public class TmdbIsTitleMatchTests
{
    // ── Exact / case-insensitive ──────────────────────────────────────────────

    [Fact]
    public void ExactMatch_ReturnsTrue()
        => Assert.True(TmdbSearchService.IsTitleMatch("Tensura", "Tensura", null));

    [Fact]
    public void CaseInsensitiveMatch_ReturnsTrue()
        => Assert.True(TmdbSearchService.IsTitleMatch("tensura", "TENSURA", null));

    [Fact]
    public void MatchAgainstLocalName_ReturnsTrue()
        => Assert.True(TmdbSearchService.IsTitleMatch("Tensura", null, "Tensura"));

    [Fact]
    public void BothNamesNull_ReturnsFalse()
        => Assert.False(TmdbSearchService.IsTitleMatch("Tensura", null, null));

    [Fact]
    public void NoMatch_ReturnsFalse()
        => Assert.False(TmdbSearchService.IsTitleMatch("Tensura", "The Slime Diaries", null));

    // ── Spinoff rejection (the motivating bug) ────────────────────────────────

    [Fact]
    public void SpinoffTitle_WithParentAsPrefix_ReturnsFalse()
        => Assert.False(TmdbSearchService.IsTitleMatch(
            "転生したらスライムだった件",
            "転生したらスライムだった件 転スラ日記",
            "The Slime Diaries: That Time I Got Reincarnated as a Slime"));

    [Fact]
    public void RootTitle_MatchesRootTmdbEntry_ReturnsTrue()
        => Assert.True(TmdbSearchService.IsTitleMatch(
            "転生したらスライムだった件",
            "転生したらスライムだった件",
            "That Time I Got Reincarnated as a Slime"));

    // ── Punctuation normalization ─────────────────────────────────────────────

    [Fact]
    public void FullWidthPunctuation_InQuery_MatchesHalfWidth()
        => Assert.True(TmdbSearchService.IsTitleMatch("Re:Zero！", "Re:Zero!", null));

    [Fact]
    public void FullWidthPunctuation_InCandidate_MatchesHalfWidth()
        => Assert.True(TmdbSearchService.IsTitleMatch("Re:Zero!", "Re:Zero！", null));

    [Fact]
    public void WaveDash_InQuery_NormalizedBeforeMatch()
        => Assert.True(TmdbSearchService.IsTitleMatch("A〜B", "A B", null));

    // ── Mixed-script split ────────────────────────────────────────────────────

    [Fact]
    public void MixedScript_CjkPartMatchesOriginalName()
        => Assert.True(TmdbSearchService.IsTitleMatch(
            "ONE PUNCH MAN ワンパンマン",
            "ワンパンマン",   // Japanese OriginalName
            null));

    [Fact]
    public void MixedScript_LatinPartMatchesLocalName()
        => Assert.True(TmdbSearchService.IsTitleMatch(
            "ONE PUNCH MAN ワンパンマン",
            null,
            "ONE PUNCH MAN")); // English Name

    [Fact]
    public void MixedScript_NeitherPartMatches_ReturnsFalse()
        => Assert.False(TmdbSearchService.IsTitleMatch(
            "ONE PUNCH MAN ワンパンマン",
            "Mob Psycho 100",
            "モブサイコ100"));

    [Fact]
    public void PurelyLatinQuery_NoMixedScriptFallback_ReturnsFalse()
        => Assert.False(TmdbSearchService.IsTitleMatch("ONE PUNCH MAN", "ワンパンマン", null));

    [Fact]
    public void PurelyJapaneseQuery_NoMixedScriptFallback_ReturnsFalse()
        => Assert.False(TmdbSearchService.IsTitleMatch("ワンパンマン", "ONE PUNCH MAN", null));
}
