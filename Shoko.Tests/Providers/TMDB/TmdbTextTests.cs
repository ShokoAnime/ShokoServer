using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

using ListedText = Shoko.Server.Providers.TMDB.TmdbTextListing.ListedText;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Covers how TMDB's titles and overviews are stored beside the English text
/// kept on each entity's row, and read back through the text manager in the
/// order TMDB listed them.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class TmdbTextTests
{
    #region Helpers

    private static ListedText Listed(string language, string country, string value)
        => new(language, country, value);

    private static TitleStub Stored(string language, string country, string value, int ordering)
        => new OrderedTitle
        {
            Source = MetadataSource.TMDB,
            Language = TmdbTextListing.Language(language, country),
            LanguageCode = language,
            CountryCode = country,
            Value = value,
            Type = TitleType.Official,
            Position = ordering,
        };

    /// <summary>
    /// A stored title with its position, as the text cache hands it out.
    /// </summary>
    private sealed class OrderedTitle : TitleStub, ITitle
    {
        public int Position { get; init; }

        int IText.Ordering => Position;
    }

    private static string[] Values(IEnumerable<ListedText> texts)
        => [.. texts.Select(text => $"{text.LanguageCode}-{text.CountryCode}:{text.Value}")];

    /// <summary>
    /// A text store over an in-memory cache, with a text manager over it put
    /// in place for the models.
    /// </summary>
    private sealed class World : System.IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public MetadataTextStore Store { get; } = new(new TextCache(), new CacheOnlyRowWriter());

        public World()
            => _scope = new RepoFactoryScope().Set(TestTextManager.Build(Store));

        public void Dispose()
            => _scope.Dispose();
    }

    #endregion

    #region Planning

    [Fact]
    public void AFirstRefreshStoresTmdbsOrderWithoutTheEnglishDefault()
    {
        var (texts, gap) = TmdbTextListing.Plan(
            new List<ITitle>(),
            false,
            [Listed("ja", "JP", "番組"), Listed("en", "US", "The Show"), Listed("fr", "FR", "Le Show")],
            "The Show"
        );

        Assert.Equal(["ja-JP:番組", "fr-FR:Le Show"], Values(texts));
        Assert.Equal(1, gap);
    }

    [Fact]
    public void AnEnglishTextThatDiffersFromTheRowIsStoredAndNotListedAsTheDefault()
    {
        var (texts, gap) = TmdbTextListing.Plan(new List<ITitle>(), false, [Listed("en", "US", "Original"), Listed("de", "DE", "Die Show")], "Translated");

        Assert.Equal(["en-US:Original", "de-DE:Die Show"], Values(texts));
        Assert.Null(gap);
    }

    [Fact]
    public void ARefreshKeepsTheStoredOrderAndAddsNewTextsLast()
    {
        // Stored: ja at 0, the English default at 1 (the gap), fr at 2.
        IReadOnlyList<ITitle> stored = [Stored("ja", "JP", "番組", 0), Stored("fr", "FR", "Le Show", 2)];
        var (texts, gap) = TmdbTextListing.Plan(
            stored,
            true,
            [Listed("de", "DE", "Die Show"), Listed("fr", "FR", "Le Nouveau Show"), Listed("en", "US", "The Show"), Listed("ja", "JP", "番組")],
            "The Show"
        );

        Assert.Equal(["ja-JP:番組", "fr-FR:Le Nouveau Show", "de-DE:Die Show"], Values(texts));
        Assert.Equal(1, gap);
    }

    [Fact]
    public void AnEnglishDefaultListedLastStaysLast()
    {
        IReadOnlyList<ITitle> stored = [Stored("fr", "FR", "Le Film", 0), Stored("ja", "JP", "映画", 1)];
        var (texts, gap) = TmdbTextListing.Plan(stored, true, [Listed("en", "US", "The Film"), Listed("ja", "JP", "映画"), Listed("fr", "FR", "Le Film")], "The Film");

        Assert.Equal(["fr-FR:Le Film", "ja-JP:映画"], Values(texts));
        Assert.Equal(2, gap);
    }

    [Fact]
    public void TextsTmdbNoLongerListsAreDropped()
    {
        IReadOnlyList<ITitle> stored = [Stored("ja", "JP", "番組", 0), Stored("fr", "FR", "Le Show", 2)];
        var (texts, gap) = TmdbTextListing.Plan(stored, true, [Listed("en", "US", "The Show")], "The Show");

        Assert.Empty(texts);
        Assert.Equal(0, gap);
    }

    [Fact]
    public void AnEpisodesGenericTitleWithItsOwnNumberIsNotStored()
    {
        var (texts, gap) = TmdbTextListing.Plan(
            new List<ITitle>(),
            false,
            [Listed("en", "US", "Episode 5"), Listed("ja", "JP", "第5話"), Listed("ko", "KR", "진짜 제목"), Listed("zh", "CN", "第7集"), Listed("de", "DE", "Folge 5")],
            "Episode 5",
            5
        );

        Assert.Equal(["ko-KR:진짜 제목", "zh-CN:第7集"], Values(texts));
        Assert.Equal(0, gap);

        // Only an episode's titles are checked.
        (texts, _) = TmdbTextListing.Plan(new List<ITitle>(), false, [Listed("ja", "JP", "第5話")], "Episode 5");
        Assert.Equal(["ja-JP:第5話"], Values(texts));
    }

    #endregion

    #region Reading

    [Fact]
    public void AShowListsItsEnglishTitleWhereTmdbListedIt()
    {
        using var world = new World();
        var show = new TMDB_Show(1) { EnglishTitle = "The Show", EnglishOverview = "An overview.", EnglishTitleListed = true, EnglishOverviewListed = true };
        world.Store.WriteListedTexts(
        [
            new(
                ((IMetadata)show).ID,
                [TmdbTextListing.ToTitle(Listed("ja", "JP", "番組")), TmdbTextListing.ToTitle(Listed("fr", "FR", "Le Show"))],
                1,
                [TmdbTextListing.ToOverview(Listed("de", "DE", "Eine Übersicht."))],
                1
            ),
        ]);

        Assert.Equal(["番組", "The Show", "Le Show"], show.GetAllTitles().Select(title => title.Value));
        Assert.Equal(["Eine Übersicht.", "An overview."], show.GetAllOverviews().Select(overview => overview.Value));
        var english = show.GetAllTitles()[1];
        Assert.True(english.IsInlineDefault);
        Assert.Equal(TitleLanguage.EnglishAmerican, english.Language);
        Assert.Equal("US", english.CountryCode);
        Assert.Equal("The Show", ((IWithTitles)show).DefaultTitle.Value);
    }

    [Fact]
    public void AnEnglishTextTmdbDidNotListIsLeftOutButStaysTheFallback()
    {
        using var world = new World();
        var show = new TMDB_Show(2) { EnglishTitle = "The Show", EnglishOverview = "An overview." };
        world.Store.WriteListedTexts(
        [
            new(((IMetadata)show).ID, [TmdbTextListing.ToTitle(Listed("ja", "JP", "番組"))], null, [TmdbTextListing.ToOverview(Listed("ja", "JP", "概要"))], null),
        ]);

        Assert.Equal(["番組"], show.GetAllTitles().Select(title => title.Value));
        Assert.Equal(["概要"], show.GetAllOverviews().Select(overview => overview.Value));
        Assert.Equal("The Show", ((IWithTitles)show).DefaultTitle.Value);
        Assert.Equal("An overview.", ((IWithOverviews)show).DefaultOverview!.Value);
        Assert.False(string.IsNullOrEmpty(show.GetPreferredTitle().Value));
    }

    [Fact]
    public void AnEntityWithNoEnglishTextFallsBackToAnEmptyOne()
    {
        using var world = new World();
        var movie = new TMDB_Movie(3) { EnglishTitle = string.Empty, EnglishOverview = string.Empty };

        Assert.Empty(movie.GetAllTitles());
        Assert.Equal(string.Empty, movie.GetPreferredTitle().Value);
        Assert.Equal(string.Empty, movie.GetPreferredOverview().Value);
        Assert.Equal(TitleLanguage.EnglishAmerican, movie.GetPreferredOverview().Language);
    }

    [Fact]
    public void APersonsOtherNamesAndBiographiesReadBackInOrder()
    {
        using var world = new World();
        var person = new TMDB_Person(4) { EnglishName = "Some Person", EnglishBiography = "A life.", EnglishOverviewListed = true };
        world.Store.WriteListedTexts(
        [
            new(
                ((IMetadata)person).ID,
                [
                    new TitleStub { Source = MetadataSource.TMDB, Language = TitleLanguage.Unknown, LanguageCode = "unk", Value = "First", Type = TitleType.Synonym },
                    new TitleStub { Source = MetadataSource.TMDB, Language = TitleLanguage.Unknown, LanguageCode = "unk", Value = "Second", Type = TitleType.Synonym },
                ],
                null,
                [TmdbTextListing.ToOverview(Listed("ja", "JP", "経歴"))],
                1
            ),
        ]);

        Assert.Equal(["First", "Second"], ((ICreator)person).AlternativeNames.Select(name => name.Value));
        Assert.Equal(["経歴", "A life."], ((IWithOverviews)person).Overviews.Select(overview => overview.Value));
    }

    [Fact]
    public void TmdbsTitlesAreAllOfficial()
    {
        using var world = new World();
        var show = new TMDB_Show(5) { EnglishTitle = "The Show", EnglishTitleListed = true };
        world.Store.WriteListedTexts([new(((IMetadata)show).ID, [TmdbTextListing.ToTitle(Listed("ja", "JP", "番組"))], 0, [], null)]);

        Assert.Equal(["The Show", "番組"], show.GetAllTitles().Select(title => title.Value));
        Assert.All(show.GetAllTitles(), title => Assert.Equal(TitleType.Official, title.Type));
        Assert.Equal(TitleType.Official, ((IWithTitles)show).DefaultTitle.Type);
    }

    [Fact]
    public void AnAlternateOrderingSeasonListsItsName()
    {
        using var world = new World();
        var season = new TMDB_AlternateOrdering_Season("group") { EnglishTitle = "Arc One" };

        Assert.Equal("Arc One", Assert.Single(season.GetAllTitles()).Value);
    }

    #endregion

    #region Choosing

    public static TheoryData<bool, bool> BoolPairs => new()
    {
        { false, false },
        { false, true },
        { true, false },
        { true, true },
    };

    /// <summary>
    /// The candidates and choice the text manager builds for a TMDB entry
    /// with a Japanese title and the English title on its row.
    /// </summary>
    private static (List<ITitle> Candidates, TitleChoice Choice) EnglishOnTheRow(bool englishListed, bool tmdbRanked, TitleLanguage[] languages)
    {
        var english = TmdbInlineText.Title("The Show")!;
        var japanese = TmdbTextListing.ToTitle(Listed("ja", "JP", "番組"));
        List<ITitle> candidates = englishListed ? [japanese, english] : [japanese];
        MetadataSource[] sources = tmdbRanked ? [MetadataSource.AniDB, MetadataSource.TMDB, MetadataSource.User] : [MetadataSource.AniDB, MetadataSource.User];
        return (candidates, new(languages, sources, false, false, Main: english));
    }

    [Theory]
    [MemberData(nameof(BoolPairs))]
    public void AnUnlistedEnglishTitleIsNotChosenForEnglish(bool episode, bool tmdbRanked)
    {
        var (candidates, choice) = EnglishOnTheRow(false, tmdbRanked, [TitleLanguage.English, TitleLanguage.Japanese]);

        var chosen = TextChooser.ChooseStoredTitle(candidates, MetadataSource.TMDB, choice with { RankGeneric = episode });

        Assert.Equal("番組", chosen?.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AListedEnglishTitleIsChosenForEnglish(bool episode)
    {
        var (candidates, choice) = EnglishOnTheRow(true, true, [TitleLanguage.English, TitleLanguage.Japanese]);

        var chosen = TextChooser.ChooseStoredTitle(candidates, MetadataSource.TMDB, choice with { RankGeneric = episode });

        Assert.Equal("The Show", chosen?.Value);
    }

    [Theory]
    [MemberData(nameof(BoolPairs))]
    public void TheEnglishTitleOnTheRowAnswersMainListedOrNot(bool listed, bool tmdbRanked)
    {
        var (candidates, choice) = EnglishOnTheRow(listed, tmdbRanked, [TitleLanguage.Main, TitleLanguage.Japanese]);

        var chosen = TextChooser.ChooseStoredTitle(candidates, MetadataSource.TMDB, choice);

        Assert.Equal("The Show", chosen?.Value);
        Assert.True(chosen!.IsInlineDefault);
    }

    [Fact]
    public void AnUnlistedEnglishTitleWithNoOtherLanguageLeavesTheFallbackToTheModel()
    {
        using var world = new World();
        var (candidates, choice) = EnglishOnTheRow(false, true, [TitleLanguage.English, TitleLanguage.German]);

        Assert.Null(TextChooser.ChooseStoredTitle(candidates, MetadataSource.TMDB, choice));
        Assert.Equal("The Show", new TMDB_Show(6) { EnglishTitle = "The Show" }.GetPreferredTitle().Value);
    }

    #endregion
}
