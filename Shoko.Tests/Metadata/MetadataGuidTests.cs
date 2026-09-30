using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers <see cref="MetadataGuid"/>: building one, the text form and how it
/// is parsed, numeric IDs, equality and ordering.
/// </summary>
/// <remarks>
/// The source and entity type registries are process-global and never forget
/// a value, so every test that needs a value of its own prefixes it with
/// <c>mg-</c> and the test's name, and can run in parallel with the rest.
/// </remarks>
public class MetadataGuidTests
{
    #region Constructor

    [Theory]
    [InlineData("a/b/c")]
    [InlineData("tt0111161")]
    [InlineData("with inner space")]
    [InlineData("://")]
    [InlineData("x")]
    public void TheConstructorTakesAnyIDWithoutWhitespaceAtTheEnds(string id)
        => Assert.Equal(id, new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, id).ID);

    [Fact]
    public void TheConstructorTakesAnIDOfTheMostCharacters()
    {
        var id = new string('a', MetadataGuid.MaxIDLength);

        Assert.Equal(id, new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, id).ID);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("\t1")]
    [InlineData("1\n")]
    public void TheConstructorRefusesAnInvalidID(string id)
        => Assert.Throws<ArgumentException>(() => new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, id));

    [Fact]
    public void TheConstructorRefusesATooLongID()
        => Assert.Throws<ArgumentException>(() => new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, new string('1', MetadataGuid.MaxIDLength + 1)));

    [Fact]
    public void TheConstructorTakesUnregisteredSourcesAndKinds()
    {
        var guid = new MetadataGuid(MetadataSource.Parse("mg-ctor-source"), MetadataEntityType.Parse("mg-ctor-kind"), "7");

        Assert.False(guid.Source.IsRegistered);
        Assert.False(guid.EntityType.IsRegistered);
        Assert.Equal("mg-ctor-source://mg-ctor-kind/7", guid.ToString());
    }

    #endregion

    #region For

    [Theory]
    [InlineData("anidb", "series", "1", "anidb://series/1")]
    [InlineData("AniDB", "Show", "1", "anidb://series/1")]
    [InlineData("TheMovieDB", "BoxSet", "10", "tmdb://collection/10")]
    [InlineData("tmdb", "company", "a/b", "tmdb://studio/a/b")]
    public void ForReadsRegisteredValuesAndAliases(string source, string entityType, string id, string expected)
        => Assert.Equal(expected, MetadataGuid.For(source, entityType, id).ToString());

    [Fact]
    public void ForRefusesAnUnregisteredSourceOrKind()
    {
        Assert.Throws<KeyNotFoundException>(() => MetadataGuid.For("mg-for-source", "series", "1"));
        Assert.Throws<KeyNotFoundException>(() => MetadataGuid.For("anidb", "mg-for-kind", "1"));
    }

    [Fact]
    public void ForRefusesAnInvalidID()
        => Assert.Throws<ArgumentException>(() => MetadataGuid.For("anidb", "series", " 1"));

    #endregion

    #region Parsing

    [Theory]
    [InlineData("anidb://series/1", "anidb", "series", "1")]
    [InlineData("ANIDB://SERIES/1", "anidb", "series", "1")]
    [InlineData("AniDB://Show/1", "anidb", "series", "1")]
    [InlineData("themoviedb://movie/603", "tmdb", "movie", "603")]
    [InlineData("tmdb://franchise/10", "tmdb", "collection", "10")]
    [InlineData("tmdb://episode/a/b/c", "tmdb", "episode", "a/b/c")]
    [InlineData("tmdb://episode//x", "tmdb", "episode", "/x")]
    [InlineData("tmdb://episode/a://b", "tmdb", "episode", "a://b")]
    [InlineData("tmdb://episode/Mixed Case", "tmdb", "episode", "Mixed Case")]
    [InlineData("  anidb://series/1  ", "anidb", "series", "1")]
    public void ParseReadsTheTextForm(string text, string source, string entityType, string id)
    {
        var guid = MetadataGuid.Parse(text);

        Assert.Same(MetadataSource.Get(source), guid.Source);
        Assert.Same(MetadataEntityType.Get(entityType), guid.EntityType);
        Assert.Equal(id, guid.ID);
        Assert.Equal($"{source}://{entityType}/{id}", guid.ToString());
    }

    [Fact]
    public void ParseReadsUnregisteredSourcesAndKinds()
    {
        var guid = MetadataGuid.Parse("MG-Parse-Source://MG-Parse-Kind/42");

        Assert.Equal("mg-parse-source", guid.Source.Value);
        Assert.Equal("mg-parse-kind", guid.EntityType.Value);
        Assert.False(guid.Source.IsRegistered);
        Assert.False(guid.EntityType.IsRegistered);
        Assert.Equal("mg-parse-source://mg-parse-kind/42", guid.ToString());
    }

    [Fact]
    public void ParseReadsUnderscoresAsHyphens()
    {
        var registered = MetadataGuid.Parse("TEST_PLUGIN://SERIES/7");
        var unregistered = MetadataGuid.Parse("mg_underscore_source://mg_underscore_kind/8");

        Assert.Equal(TestSources.Plugin, registered.Source);
        Assert.Equal(MetadataEntityType.Series, registered.EntityType);
        Assert.Equal("test-plugin://series/7", registered.ToString());
        Assert.Equal("mg-underscore-source://mg-underscore-kind/8", unregistered.ToString());
        Assert.False(unregistered.Source.IsRegistered);
    }

    public static TheoryData<string?> InvalidTexts() => new()
    {
        null!,
        "",
        "   ",
        "anidb",
        "anidb://",
        "anidb://series",
        "anidb://series/",
        "anidb://series/ 1",
        "://series/1",
        "anidb:///1",
        "anidb:/series/1",
        "anidb//series/1",
        "anidb:series/1",
        "ani db://series/1",
        "anidb ://series/1",
        "anidb:// series/1",
        "anidb://series /1",
        "not_valid_://series/1",
        "anidb://not_valid_/1",
        "anidb://series/" + new string('1', MetadataGuid.MaxIDLength + 1),
    };

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public void ParseRefusesInvalidText(string? text)
    {
        Assert.False(MetadataGuid.TryParse(text, out var guid));
        Assert.Null(guid);
        Assert.False(MetadataGuid.TryParse(text, null, out _));
        Assert.Throws<FormatException>(() => MetadataGuid.Parse(text!));
    }

    [Fact]
    public void TheTextFormRoundTrips()
    {
        var guids = new[]
        {
            new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "1"),
            new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "a/b://c"),
            new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Channel, "with inner space"),
            new MetadataGuid(MetadataSource.Parse("mg-round-trip"), MetadataEntityType.Parse("mg-round-trip-kind"), "x"),
        };

        foreach (var guid in guids)
            Assert.Equal(guid, MetadataGuid.Parse(guid.ToString()));
    }

    [Fact]
    public void AFailedParseLeavesNoUnregisteredSourceBehind()
    {
        Assert.False(MetadataGuid.TryParse("mg-failed-source://not_valid_/1", out _));

        var source = MetadataSource.Register("Mg Failed Source Other", "mg-failed-source-other", ["mg-failed-source"]);
        Assert.Same(source, MetadataSource.Get("mg-failed-source"));
    }

    [Fact]
    public void AFailedParseLeavesNoUnregisteredKindBehind()
    {
        Assert.False(MetadataGuid.TryParse("not_valid_://mg-failed-kind/1", out _));

        var entityType = MetadataEntityType.Register("Mg Failed Kind Other", "mg-failed-kind-other", ["mg-failed-kind"]);
        Assert.Same(entityType, MetadataEntityType.Get("mg-failed-kind"));
    }

    #endregion

    #region Numeric IDs

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("1234567890", true)]
    [InlineData("99999999999999999999999999999999999999999", true)]
    [InlineData("00", false)]
    [InlineData("01", false)]
    [InlineData("-1", false)]
    [InlineData("+1", false)]
    [InlineData("1.0", false)]
    [InlineData("1e3", false)]
    [InlineData("1_000", false)]
    [InlineData("0x1F", false)]
    [InlineData("12a", false)]
    [InlineData("abc", false)]
    [InlineData("١٢٣", false)]
    [InlineData("１２", false)]
    public void IsNumericIDIsTrueForCanonicalNonNegativeIntegersOnly(string id, bool expected)
        => Assert.Equal(expected, new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, id).IsNumericID);

    [Fact]
    public void ANumericIDIsReadIntoATypeItFits()
    {
        var guid = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "256");

        Assert.True(guid.TryGetNumericID<int>(out var asInt));
        Assert.Equal(256, asInt);
        Assert.False(guid.TryGetNumericID<byte>(out _));
        Assert.Throws<OverflowException>(() => guid.GetNumericID<byte>());
    }

    [Fact]
    public void ACharIsReadAsANumber()
    {
        Assert.True(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "123").TryGetNumericID<char>(out var value));
        Assert.Equal((char)123, value);
        Assert.False(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "65536").TryGetNumericID<char>(out _));
        Assert.Equal((char)123, new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "123").GetNumericID<char>());
        Assert.Throws<OverflowException>(() => new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "65536").GetNumericID<char>());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("01")]
    [InlineData("-1")]
    public void TryGetNumericIDGivesZeroForANonNumericID(string id)
    {
        Assert.False(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, id).TryGetNumericID<int>(out var value));
        Assert.Equal(0, value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("007")]
    [InlineData("-5")]
    public void GetNumericIDThrowsAFormatExceptionForANonNumericID(string id)
        => Assert.Throws<FormatException>(() => new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, id).GetNumericID<int>());

    #endregion

    #region Equality

    [Fact]
    public void IdentifiersNamingTheSameEntryAreEqual()
    {
        var constructed = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "1");
        var parsed = MetadataGuid.Parse("AniDB://Anime/1");
        var built = MetadataGuid.For("anidb", "show", "1");

        Assert.Equal(constructed, parsed);
        Assert.Equal(constructed, built);
        Assert.True(constructed == parsed);
        Assert.False(constructed != parsed);
        Assert.True(constructed.Equals((object)parsed));
        Assert.Equal(constructed.GetHashCode(), parsed.GetHashCode());
        Assert.Single(new HashSet<MetadataGuid> { constructed, parsed, built });
    }

    [Fact]
    public void AnyDifferentPartMakesThemUnequal()
    {
        var guid = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "abc");

        Assert.NotEqual(guid, new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "abc"));
        Assert.NotEqual(guid, new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, "abc"));
        Assert.NotEqual(guid, new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "ABC"));
        Assert.NotEqual(guid, new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "abd"));
    }

    [Fact]
    public void AnUnregisteredPartEqualsItsLaterRegistration()
    {
        var early = MetadataGuid.Parse("anidb://mg-equal-kind/1");
        var entityType = MetadataEntityType.Register("MgEqualKind", "mg-equal-kind");
        var late = new MetadataGuid(MetadataSource.AniDB, entityType, "1");

        Assert.Equal(early, late);
        Assert.Equal(early.GetHashCode(), late.GetHashCode());
    }

    #endregion

    #region Ordering

    [Fact]
    public void IdentifiersSortBySourceThenKindThenID()
    {
        var expected = new[]
        {
            "anidb://collection/1",
            "anidb://series/1",
            "anidb://series/2",
            "anidb://series/10",
            "anidb://series/100",
            "anidb://series/a",
            "anidb://series/b",
            "anidb://episode/1",
            "tmdb://series/1",
            "shoko://series/1",
        }.Select(text => MetadataGuid.Parse(text)).ToList();

        var shuffled = expected.AsEnumerable().Reverse().OrderBy(guid => guid.ID.Length).ToList();
        shuffled.Sort();

        Assert.Equal(expected, shuffled);
        Assert.Equal(0, expected[1].CompareTo(MetadataGuid.Parse("AniDB://Show/1")));
        Assert.True(expected[0].CompareTo(null) > 0);
    }

    [Fact]
    public void NumericIDsComeBeforeTheOthers()
    {
        var numeric = MetadataGuid.Parse("anidb://series/999");
        var leadingZero = MetadataGuid.Parse("anidb://series/01");
        var text = MetadataGuid.Parse("anidb://series/1a");

        Assert.True(numeric.CompareTo(leadingZero) < 0);
        Assert.True(numeric.CompareTo(text) < 0);
        Assert.True(leadingZero.CompareTo(text) < 0);
        Assert.True(text.CompareTo(numeric) > 0);
    }

    #endregion
}
