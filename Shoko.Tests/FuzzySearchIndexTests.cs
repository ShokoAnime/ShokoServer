using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Utilities;
using Xunit;

// ReSharper disable StringLiteralTypo

namespace Shoko.Tests;

/// <summary>
/// Tests for <see cref="FuzzySearchIndex{T}"/> and the helper methods in <see cref="SeriesSearch"/>
/// that power the trigram-indexed fuzzy search. Title strings are taken verbatim from the
/// AniDB_Anime test dataset (same data as AniDB_Anime.json).
/// </summary>
public class FuzzySearchIndexTests
{
    private record Anime(int AnimeId, string[] Titles);

    // Titles taken verbatim from AniDB_Anime.json AllTitles fields.
    private static readonly Anime[] s_testAnime =
    [
        // AnimeID 18289 — Ramen Aka Neko
        new(18289, ["Ramen Aka Neko", "Red Cat Ramen", "ラーメン赤猫"]),
        // AnimeID 18097 — Tasogare Outfocus
        new(18097, ["Tasogare Outfocus", "Twilight Out of Focus", "黄昏アウトフォーカス"]),
        // AnimeID 18067 — Kimetsu no Yaiba: Hashira Geiko Hen
        new(18067, ["Kimetsu no Yaiba: Hashira Geiko Hen", "鬼滅の刃 柱稽古編", "Demon Slayer: Hashira Training Arc", "鬼灭之刃 柱训练篇"]),
        // AnimeID 18527 — Tensui no Sakuna-hime
        new(18527, ["Tensui no Sakuna-hime", "Sakuna: Of Rice and Ruin", "天穂のサクナヒメ"]),
        // AnimeID 16383 — Grimm Kumikyoku
        new(16383, ["Grimm Kumikyoku", "グリム組曲", "The Grimm Variations", "Baśnie braci Grimm: Wariacje", "As Variações de Grimm", "格林童话变奏曲"]),
        // AnimeID 17931 — Boku no Hero Academia (2024)
        new(17931, ["Boku no Hero Academia (2024)", "My Hero Academia Season 7", "僕のヒーローアカデミア (2024)"]),
        // AnimeID 17955 — Gekijouban Blue Lock: Episode Nagi
        new(17955, ["Gekijouban Blue Lock: Episode Nagi", "劇場版ブルーロック -EPISODE 凪-", "Blue Lock: Episode Nagi"]),
        // AnimeID 18438 — Hazure Waku (Failure Frame)
        new(18438, ["Hazure Waku no [Joutai Ijou Skill] de Saikyou ni Natta Ore ga Subete o Juurin Suru made",
                    "Failure Frame: I Became the Strongest and Annihilated Everything with Low-Level Spells",
                    "ハズレ枠の[状態異常スキル]で最強になった俺がすべてを蹂躙するまで"]),
        // AnimeID 17819 — Sasayaku You ni Koi o Utau
        new(17819, ["Sasayaku You ni Koi o Utau", "ささやくように恋を唄う", "Whisper Me a Love Song"]),
        // AnimeID 91 — Gate Keepers
        new(91, ["Gate Keepers", "Gatekeepers", "GK", "ゲートキーパーズ"]),
        // AnimeID 2369 — Bleach
        new(2369, ["Bleach", "ブリーチ", "BLEACH", "bleach tv", "死神"]),
        // AnimeID 4515 — Bleach Movie 1 (has Hiçkimsenin with cedilla)
        new(4515, ["Gekijouban Bleach: Memories of Nobody", "Bleach Film: Hiçkimsenin Hatıraları",
                   "Bleach the Movie: Memories of Nobody", "劇場版 BLEACH MEMORIES OF NOBODY"]),
        // AnimeID 15449 — Bleach: Thousand-Year Blood War
        new(15449, ["Bleach: Sennen Kessen Hen", "BLEACH 千年血戦篇", "Bleach: Thousand-Year Blood War"]),
        // AnimeID 18086 — Oshi no Ko
        new(18086, ["\"Oshi no Ko\"", "[推しの子]", "Oshi no Ko"]),
        // AnimeID 18424 — Monogatari Series: Off & Monster Season
        new(18424, ["Monogatari Series: Off & Monster Season", "〈物語〉シリーズ オフ＆モンスターシーズン"]),
        // AnimeID 9018 — Steins;Gate (semicolon normalized to space)
        new(9018, ["Steins;Gate", "シュタインズ・ゲート"]),
    ];

    private static readonly FuzzySearchIndex<Anime> s_index = BuildIndex();

    private static FuzzySearchIndex<Anime> BuildIndex()
    {
        var idx = new FuzzySearchIndex<Anime>();
        idx.Build(s_testAnime, a => a.Titles);
        return idx;
    }

    private static IEnumerable<SeriesSearch.SearchResult<Anime>> Search(string query, bool fuzzy = true)
        => s_index.Search(query, fuzzy);

    // ── NormalizeForIndex ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Sword Art Online", "sword art online")]
    [InlineData("SWORD ART ONLINE", "sword art online")]
    [InlineData("Sword-Art_Online", "sword art online")]
    [InlineData("Sword: Art. Online!", "sword art online")]
    [InlineData("  Sword  Art  Online  ", "sword art online")]
    [InlineData("Tensui no Sakuna-hime", "tensui no sakuna hime")]
    [InlineData("Baśnie braci Grimm: Wariacje", "basnie braci grimm wariacje")]
    [InlineData("As Variações de Grimm", "as variacoes de grimm")]
    [InlineData("café", "cafe")]
    [InlineData("über", "uber")]
    [InlineData("", "")]
    public void NormalizeForIndex_ProducesExpectedString(string input, string expected)
        => Assert.Equal(expected, SeriesSearch.NormalizeForIndex(input));

    // ── IsLatinScript ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("hello world", true)]
    [InlineData("café", true)]                    // é is Latin Extended (U+00E9 < U+024F)
    [InlineData("naïve", true)]                   // ï is Latin Extended (U+00EF < U+024F)
    [InlineData("Baśnie", true)]                  // ś is Latin Extended (U+015B < U+024F)
    [InlineData("", true)]                        // no letters → trivially Latin
    [InlineData("12345 !@#", true)]               // only digits and punctuation, no letters
    [InlineData("鬼滅の刃", false)]
    [InlineData("مرحبا", false)]                  // Arabic
    [InlineData("Привет", false)]                 // Cyrillic
    [InlineData("Blue Lock エピソード", false)]   // mixed: Japanese katakana present
    public void IsLatinScript_DetectsNonLatinCharacters(string input, bool expected)
        => Assert.Equal(expected, SeriesSearch.IsLatinScript(input));

    // ── GetMaxErrors ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(11, 2)]
    [InlineData(12, 3)]
    [InlineData(20, 3)]
    [InlineData(100, 3)]
    public void GetMaxErrors_ScalesProportionallyUpToCap(int queryLength, int expectedErrors)
        => Assert.Equal(expectedErrors, SeriesSearch.GetMaxErrors(queryLength));

    // ── MinEditDistInText ────────────────────────────────────────────────────

    [Theory]
    [InlineData("demon", "demon slayer kimetsu no yaiba", 0)]  // exact prefix
    [InlineData("kimetsu", "demon slayer kimetsu no yaiba", 0)] // exact interior
    [InlineData("yaiba", "demon slayer kimetsu no yaiba", 0)]   // exact suffix
    [InlineData("cat", "xabcatx", 0)]                          // exact interior surrounded
    [InlineData("academia", "my hero academia season 7", 0)]   // exact interior
    [InlineData("kimetzu", "kimetsu no yaiba", 1)]             // z→s substitution
    [InlineData("onlne", "sword art online", 1)]               // missing 'i' (1 deletion)
    [InlineData("heroe", "my hero academia", 1)]               // extra 'e' (1 insertion)
    [InlineData("raman", "red cat ramen", 1)]                  // a→e substitution
    [InlineData("stiens", "steins gate", 1)]                   // adjacent transposition (e↔i)
    [InlineData("", "anything", 0)]                            // empty query matches anywhere
    public void MinEditDistInText_ReturnsCorrectEditCount(string query, string text, int expected)
        => Assert.Equal(expected, SeriesSearch.MinEditDistInText(query, text));

    // ── Exact and substring search ───────────────────────────────────────────

    [Theory]
    // Main title exact matches
    [InlineData("Ramen Aka Neko", 18289)]
    [InlineData("Gate Keepers", 91)]
    [InlineData("Whisper Me a Love Song", 17819)]
    // English titles (exact substring of longer AllTitles entry)
    [InlineData("Red Cat Ramen", 18289)]
    [InlineData("Demon Slayer", 18067)]
    [InlineData("My Hero Academia", 17931)]
    [InlineData("Blue Lock", 17955)]
    // Short English substring buried in a very long title
    [InlineData("Failure Frame", 18438)]
    // Abbreviation stored as its own title entry
    [InlineData("GK", 91)]
    public void Search_ExactOrSubstringTitle_FindsCorrectAnime(string query, int expectedAnimeId)
    {
        var results = Search(query).ToList();
        Assert.Contains(results, r => r.Result.AnimeId == expectedAnimeId);
    }

    // ── Case insensitivity ───────────────────────────────────────────────────

    [Theory]
    [InlineData("ramen aka neko", 18289)]
    [InlineData("RAMEN AKA NEKO", 18289)]
    [InlineData("BLUE LOCK", 17955)]
    [InlineData("my hero academia", 17931)]
    public void Search_CaseInsensitive_FindsCorrectAnime(string query, int expectedAnimeId)
    {
        var results = Search(query).ToList();
        Assert.Contains(results, r => r.Result.AnimeId == expectedAnimeId);
    }

    // ── Separator normalization ──────────────────────────────────────────────

    [Fact]
    public void Search_HyphenRemovedFromTitle_StillFindsAnime()
    {
        // "Tensui no Sakuna-hime" → normalized "tensui no sakuna hime"
        // Querying without the hyphen should match.
        var results = Search("Tensui no Sakuna hime").ToList();
        Assert.Contains(results, r => r.Result.AnimeId == 18527);
    }

    // ── Diacritic stripping ──────────────────────────────────────────────────

    [Theory]
    // "Baśnie braci Grimm: Wariacje" → "basnie braci grimm wariacje" (ś = s + combining acute U+0301)
    [InlineData("Basnie braci Grimm", 16383)]
    // "As Variações de Grimm" → "as variacoes de grimm" (ã = a + tilde U+0303, õ = o + tilde U+0303)
    [InlineData("As Variacoes de Grimm", 16383)]
    // "Bleach Film: Hiçkimsenin Hatıraları" → "bleach film hickimsenin hatıraları" (ç = c + cedilla U+0327)
    [InlineData("Hickimsenin", 4515)]
    public void Search_DiacriticStripping_FindsAnimeByBaseChars(string queryWithoutDiacritics, int expectedAnimeId)
    {
        var results = Search(queryWithoutDiacritics).ToList();
        Assert.Contains(results, r => r.Result.AnimeId == expectedAnimeId);
    }

    // ── CJK / non-Latin fallback (string-contains) ───────────────────────────

    [Theory]
    [InlineData("鬼滅の刃", 18067)]         // Kimetsu no Yaiba: "鬼滅の刃 柱稽古編" contains "鬼滅の刃"
    [InlineData("ラーメン赤猫", 18289)]     // Ramen Aka Neko exact Japanese title
    [InlineData("赤猫", 18289)]             // partial CJK: contained within "ラーメン赤猫"
    public void Search_CJKQuery_UsesContainsFallback(string query, int expectedAnimeId)
    {
        var results = Search(query).ToList();
        Assert.Contains(results, r => r.Result.AnimeId == expectedAnimeId);
    }

    // ── Fuzzy typo tolerance ─────────────────────────────────────────────────

    [Theory]
    // 1 substitution: "Raman" vs "Ramen" — query length 13, maxErrors = 3
    [InlineData("Red Cat Raman", 18289)]
    // 1 substitution: "Hiro" vs "Hero" — query length 16, maxErrors = 3
    [InlineData("My Hiro Academia", 17931)]
    // 2 edits: "Kimetzu no Yiba" vs "Kimetsu no Yaiba" — query length 15, maxErrors = 3
    [InlineData("Kimetzu no Yiba", 18067)]
    // 1 insertion: "Bleeach" vs "Bleach" — query length 7, maxErrors = 1
    [InlineData("Bleeach", 2369)]
    // 1 adjacent transposition: "stiens" vs "steins" — query length 6, maxErrors = 1
    [InlineData("Stiens Gate", 9018)]
    public void Search_FuzzyTypo_FindsCorrectAnime(string misspelledQuery, int expectedAnimeId)
    {
        var results = Search(misspelledQuery).ToList();
        Assert.Contains(results, r => r.Result.AnimeId == expectedAnimeId);
    }

    // ── Non-fuzzy mode ───────────────────────────────────────────────────────

    [Fact]
    public void Search_NonFuzzy_DoesNotMatchTypo()
    {
        // "Raman" is not a substring of "Ramen" — should return no result when fuzzy=false.
        var results = Search("Red Cat Raman", fuzzy: false).ToList();
        Assert.DoesNotContain(results, r => r.Result.AnimeId == 18289);
    }

    [Fact]
    public void Search_NonFuzzy_ExactSubstringStillMatches()
    {
        var results = Search("Red Cat Ramen", fuzzy: false).ToList();
        Assert.Contains(results, r => r.Result.AnimeId == 18289);
    }

    // ── ExactMatch flag ──────────────────────────────────────────────────────

    [Fact]
    public void Search_ExactTitleMatch_SetsExactMatchFlag()
    {
        var results = Search("Ramen Aka Neko").ToList();
        var target = results.First(r => r.Result.AnimeId == 18289);
        Assert.True(target.ExactMatch);
    }

    [Fact]
    public void Search_FuzzyTypoMatch_DoesNotSetExactMatchFlag()
    {
        // "Red Cat Raman" is 1 edit from "Red Cat Ramen" — fuzzy hit, not substring.
        var results = Search("Red Cat Raman").ToList();
        var target = results.First(r => r.Result.AnimeId == 18289);
        Assert.False(target.ExactMatch);
    }

    // ── Isolation: no cross-contamination between distinct anime ─────────────

    [Fact]
    public void Search_DistinctAnime_ResultsAreIsolated()
    {
        var failureResults = Search("Failure Frame").Select(r => r.Result.AnimeId).ToList();
        var sakunaResults = Search("Sakuna").Select(r => r.Result.AnimeId).ToList();

        Assert.Contains(18438, failureResults);
        Assert.Contains(18527, sakunaResults);
        Assert.DoesNotContain(18527, failureResults);
        Assert.DoesNotContain(18438, sakunaResults);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Search_EmptyQuery_ReturnsNoResults()
        => Assert.Empty(Search(""));

    [Fact]
    public void Search_WhitespaceOnlyQuery_ReturnsNoResults()
        => Assert.Empty(Search("   "));

    [Fact]
    public void Search_QueryWithNoMatchingTrigrams_ReturnsNoResults()
    {
        // Trigrams of this nonsense string ("zzk", "zkz", "kzq", ...) do not appear in any test title.
        Assert.Empty(Search("zzzkzqxqzqxkzqxq"));
    }
}
