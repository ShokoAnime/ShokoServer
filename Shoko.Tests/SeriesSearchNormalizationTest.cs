using System.Collections.Generic;
using Shoko.Server.Utilities;
using Xunit;

// ReSharper disable StringLiteralTypo

namespace Shoko.Tests;

public class SeriesSearchNormalizationTest
{
    public static IEnumerable<object[]> FullwidthAsciiData => new List<object[]>
    {
        // Fullwidth ASCII title matched by normal ASCII query
        new object[] { "Ｇｉｎｔａｍａ", "gintama", true },
        new object[] { "ＧＩＮＴＡＭＡ", "gintama", true },
        new object[] { "ｇｉｎｔａｍａ", "GINTAMA", true },
        // Normal ASCII title matched by fullwidth query
        new object[] { "Gintama", "Ｇｉｎｔａｍａ", true },
        // Unrelated titles should not match
        new object[] { "Ｎａｒｕｔｏ", "gintama", false },
    };

    public static IEnumerable<object[]> HalfwidthKatakanaData => new List<object[]>
    {
        // Halfwidth katakana title matched by fullwidth katakana query
        new object[] { "ｱｲｳｴｵ", "アイウエオ", true },
        // Fullwidth katakana title matched by halfwidth query
        new object[] { "アイウエオ", "ｱｲｳｴｵ", true },
    };

    public static IEnumerable<object[]> MixedData => new List<object[]>
    {
        // Mix of fullwidth and normal characters
        new object[] { "Ｇｉｎtama", "gintama", true },
        new object[] { "gintama", "Ｇｉｎtama", true },
    };

    [Theory, MemberData(nameof(FullwidthAsciiData))]
    public void FullwidthAsciiNormalization(string title, string query, bool expectMatch)
        => Assert.Equal(expectMatch, title.FuzzyMatch(query));

    [Theory, MemberData(nameof(HalfwidthKatakanaData))]
    public void HalfwidthKatakanaNormalization(string title, string query, bool expectMatch)
        => Assert.Equal(expectMatch, title.FuzzyMatch(query));

    public static IEnumerable<object[]> EllipsisData => new List<object[]>
    {
        // U+2026 ellipsis in title matched by three-period query
        new object[] { "Fullmetal Alchemist…", "Fullmetal Alchemist...", true },
        // Three-period title matched by U+2026 query
        new object[] { "Fullmetal Alchemist...", "Fullmetal Alchemist…", true },
        // Unrelated title should not match
        new object[] { "Naruto…", "Fullmetal Alchemist...", false },
    };

    [Theory, MemberData(nameof(MixedData))]
    public void MixedWidthNormalization(string title, string query, bool expectMatch)
        => Assert.Equal(expectMatch, title.FuzzyMatch(query));

    [Theory, MemberData(nameof(EllipsisData))]
    public void EllipsisNormalization(string title, string query, bool expectMatch)
        => Assert.Equal(expectMatch, title.FuzzyMatch(query));

    public static IEnumerable<object[]> TildeData => new List<object[]>
    {
        // TMDB-style "~Emphasis~" title matched by an AniDB-style ": Emphasis" query (e.g. Slime
        // (2021 Part 2) episode 10, "Demon Lords' Banquet ~Walpurgis~" vs "Demon Lords' Banquet:
        // Walpurgis"). NormalizeForIndex equality is symmetric, so one direction covers both.
        new object[] { "Demon Lords' Banquet ~Walpurgis~", "Demon Lords' Banquet: Walpurgis", true },
        // Unrelated titles should not match just because both use tildes.
        new object[] { "~Naruto~", "~Fullmetal Alchemist~", false },
        // Same normalization applies to show titles, not just episode titles (e.g. TMDB show
        // "Naruto ~Shippuden~" vs AniDB show "Naruto: Shippuden").
        new object[] { "Naruto ~Shippuden~", "Naruto: Shippuden", true },
    };

    // Via NormalizeForIndex (what .Search() uses), not FuzzyMatch — its ForceASCII pipeline already deletes '~'/'|', so it'd pass regardless of this fix.
    [Theory, MemberData(nameof(TildeData))]
    public void TildeNormalization(string title, string query, bool expectMatch)
        => Assert.Equal(expectMatch, SeriesSearch.NormalizeForIndex(title) == SeriesSearch.NormalizeForIndex(query));

    public static IEnumerable<object[]> PipeData => new List<object[]>
    {
        // AniDB-style "A: B" title matched by a TMDB-style "A | B" query (e.g. DanMachi episode 1,
        // AniDB "Bell Cranel: Adventurer" vs TMDB "Bell Cranel | Adventurer"). NormalizeForIndex
        // equality is symmetric, so one direction covers both.
        new object[] { "Bell Cranel | Adventurer", "Bell Cranel: Adventurer", true },
        // "/" (already normalized) and "|" should be interchangeable too.
        new object[] { "Berlint Panic | The Informant", "Berlint Panic / The Informant", true },
        // Unrelated titles should not match just because both use pipes.
        new object[] { "Naruto | Shippuden", "Fullmetal Alchemist | Brotherhood", false },
        // Same normalization applies to show titles, not just episode titles (e.g. TMDB show
        // "Naruto | Shippuden" vs AniDB show "Naruto: Shippuden").
        new object[] { "Naruto | Shippuden", "Naruto: Shippuden", true },
    };

    [Theory, MemberData(nameof(PipeData))]
    public void PipeNormalization(string title, string query, bool expectMatch)
        => Assert.Equal(expectMatch, SeriesSearch.NormalizeForIndex(title) == SeriesSearch.NormalizeForIndex(query));
}
