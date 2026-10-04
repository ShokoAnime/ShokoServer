using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Plugin.Tmdb.Mapping;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;
using Xunit;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The plugin's mapping rules that the fixtures do not reach: the texts,
///   the aggregated credits, the orderings and the site URLs.
/// </summary>
public sealed class TmdbMappingTests
{
    #region Texts

    [Fact]
    public void OnlyTheTranscriptionsAmongTheAlternativeTitlesAreKept()
    {
        var titles = TmdbTexts.TranscribedTitles(
            [
                new() { Iso_3166_1 = "US", Title = "Romanized Title", Type = "Romaji" },
                new() { Iso_3166_1 = "JP", Title = "Tabi no Owari", Type = "Romaji" },
                new() { Iso_3166_1 = "JP", Title = "旅の終わり", Type = "Romaji" },
                new() { Iso_3166_1 = "KR", Title = "Yeohaeng-ui Kkeut", Type = "Revised Romanization" },
            ],
            "ja"
        );

        // A generic type only counts in the original language's home country, and one per language.
        Assert.Equal([("x-jat", "Tabi no Owari"), ("x-kot", "Yeohaeng-ui Kkeut")], titles);
    }

    [Fact]
    public void TheEnglishTextFallsBackToTheEntrysOwn()
    {
        var translations = new TranslationsContainer
        {
            Translations = [new() { Iso_639_1 = "en", Iso_3166_1 = "US", Data = new() { Name = " ", Overview = "From the translation." } }],
        };

        Assert.Equal("Own name", TmdbTexts.English(translations, data => data.Name, " Own name "));
        Assert.Equal("From the translation.", TmdbTexts.English(translations, data => data.Overview, "Own overview"));
    }

    [Theory]
    [InlineData("化物語", null, "ja", null)]
    [InlineData(".hack", ".hack", "ja", null)]
    [InlineData("Another Movie", "Another Movie", "en", "Another Movie")]
    [InlineData("Le Film", null, "fr", null)]
    [InlineData("Bleach", "BLEACH", "ja", "Bleach")]
    public void TheEntrysOwnTextIsEnglishOnlyWhenItIsNoFallback(string own, string? original, string? originalLanguage, string? expected)
    {
        var translations = new TranslationsContainer
        {
            Translations = [new() { Iso_639_1 = "fr", Iso_3166_1 = "FR", Data = new() { Name = "Le Film" } }],
        };

        Assert.Equal(expected, TmdbTexts.English(translations, data => data.Name, own, original, originalLanguage));
    }

    [Fact]
    public void ASeasonsGenericNameIsPassedOnForTheCoreToDrop()
    {
        var season = new TvSeason
        {
            Id = 2001,
            SeasonNumber = 2,
            Name = "Season 2",
            Translations = new()
            {
                Translations =
                [
                    new() { Iso_639_1 = "en", Iso_3166_1 = "US", Data = new() { Name = "Season 2" } },
                    new() { Iso_639_1 = "de", Iso_3166_1 = "DE", Data = new() { Name = "Staffel 2" } },
                ],
            },
        };

        var titles = TmdbEntityMapper.ToSeasonData(season, TmdbTextLanguages.All).Titles;

        Assert.Equal(
            [("en", TitleType.Main, "Season 2"), ("de", TitleType.Official, "Staffel 2")],
            titles.Select(title => (title.LanguageCode, title.Type, title.Value))
        );
    }

    [Fact]
    public void WithoutAnEnglishTitleTheOneTmdbNamesTheEntryByIsTheMainOne()
    {
        var translations = new TranslationsContainer
        {
            Translations = [new() { Iso_639_1 = "ko", Iso_3166_1 = "KR", Data = new() { Name = "로보트 태권V" } }],
        };
        var english = TmdbTexts.English(translations, data => data.Name, "로보트 태권V");

        var titles = TmdbTexts.Titles(english, null, null, translations, null, null, ownName: "로보트 태권V");

        Assert.Equal([("ko", TitleType.Main, "로보트 태권V")], titles.Select(title => (title.LanguageCode, title.Type, title.Value)));
        Assert.Equal(
            [("ko", TitleType.Official, "로보트 태권V"), ("unk", TitleType.Main, "ビックリマン")],
            TmdbTexts.Titles(null, null, null, translations, null, null, ownName: "ビックリマン").Select(title => (title.LanguageCode, title.Type, title.Value)));
    }

    [Theory]
    [InlineData("xx", null, TitleLanguage.None)]
    [InlineData("cn", null, TitleLanguage.Chinese)]
    [InlineData("ab", null, TitleLanguage.Unknown)]
    [InlineData("en", "US", TitleLanguage.EnglishAmerican)]
    [InlineData("pt", "BR", TitleLanguage.BrazilianPortuguese)]
    public void TmdbsOwnLanguageCodesAreRead(string code, string? country, TitleLanguage expected)
        => Assert.Equal(expected, TmdbTexts.Language(code, country));

    #endregion

    #region Credits

    [Theory]
    [InlineData(false, new[] { "Frieren (young)", "Fern", "Frieren", "Heiter" })]
    [InlineData(true, new[] { "Frieren (young)", "Frieren", "Fern", "Heiter" })]
    public void AnAggregatedCastHasEachRoleOnceAtItsFirstPlace(bool season, string[] expected)
    {
        var frieren = Cast(1, "Frieren");
        var fern = Cast(2, "Fern");
        var guest = Cast(3, "Heiter") with { RoleNotes = TmdbCredits.GuestStarNotes };
        var dual = Cast(1, "Frieren (young)");

        var aggregated = TmdbCredits.AggregateCast([[fern, frieren, guest], [frieren, fern], [dual]], season);

        Assert.Equal(expected, aggregated.Select(credit => credit.Name));
    }

    [Fact]
    public void ASeasonsCrewHasEachJobOnceByJob()
    {
        var director = new MetadataCrewData { CreatorID = TmdbIds.Creator(10), Name = "Directing, Director" };
        var composer = new MetadataCrewData { CreatorID = TmdbIds.Creator(11), Name = "Sound, Original Music Composer" };

        var aggregated = TmdbCredits.AggregateCrew([[composer, director], [director]]);

        Assert.Equal([director.CreatorID, composer.CreatorID], aggregated.Select(credit => credit.CreatorID));
    }

    [Fact]
    public void AStoredCastCreditIsWrittenAgainAsItWas()
    {
        var stored = Mock.Of<ICast>(cast =>
            cast.CreatorID == TmdbIds.Creator(1) &&
            cast.Name == "Frieren" &&
            cast.Description == TmdbCredits.GuestStarNotes &&
            cast.LanguageCode == "ja" &&
            cast.Creator == Mock.Of<ICreator>(creator => creator.Name == "Atsumi Tanezaki"));

        Assert.Equal(
            new MetadataCastData { CreatorID = TmdbIds.Creator(1), CreatorName = "Atsumi Tanezaki", Name = "Frieren", LanguageCode = "ja", RoleNotes = TmdbCredits.GuestStarNotes },
            TmdbCredits.FromStored(stored)
        );
    }

    private static MetadataCastData Cast(int personID, string role)
        => new() { CreatorID = TmdbIds.Creator(personID), Name = role };

    #endregion

    #region Orderings

    [Fact]
    public void ACollectionNoneOfWhoseEpisodesIsStoredIsNoOrdering()
    {
        var collection = new TvGroupCollection
        {
            Id = "c1",
            Name = "Collection",
            Type = TvGroupType.DVD,
            Groups = [new() { Id = "g1", Name = "Disc 1", Order = 1, Episodes = [new() { Id = 1, Order = 0 }] }],
        };

        Assert.Null(TmdbOrderings.ToOrderingData(1001, collection, new HashSet<MetadataGuid>()));
        Assert.Equal(OrderingType.DVD, TmdbOrderings.ToOrderingData(1001, collection, new HashSet<MetadataGuid> { TmdbIds.Episode(1) })?.Type);
    }

    [Fact]
    public void OnlyOneGroupHoldsTheSpecials()
    {
        var collection = new TvGroupCollection
        {
            Id = "c1",
            Name = "Collection",
            Groups =
            [
                new() { Id = "g1", Name = "Specials", Order = 0, Episodes = [new() { Id = 1, Order = 0 }] },
                new() { Id = "g2", Name = "Extras", Order = 0, Episodes = [new() { Id = 2, Order = 0 }] },
            ],
        };

        var ordering = TmdbOrderings.ToOrderingData(1001, collection, new HashSet<MetadataGuid> { TmdbIds.Episode(1), TmdbIds.Episode(2) });

        Assert.Equal([true, false], ordering!.Groups.Select(group => group.IsSpecial));
    }

    [Fact]
    public void AGroupKeepsTmdbsNumberAsItsSeasonNumber()
    {
        var collection = new TvGroupCollection
        {
            Id = "c1",
            Name = "Air Date (US)",
            Groups =
            [
                new() { Id = "g3", Name = "Season 3", Order = 3, Episodes = [new() { Id = 3, Order = 0 }] },
                new() { Id = "g0", Name = "Specials", Order = 0, Episodes = [new() { Id = 1, Order = 0 }] },
                new() { Id = "g0b", Name = "Extras", Order = 0, Episodes = [new() { Id = 2, Order = 0 }] },
            ],
        };

        var ordering = TmdbOrderings.ToOrderingData(1001, collection, new HashSet<MetadataGuid> { TmdbIds.Episode(1), TmdbIds.Episode(2), TmdbIds.Episode(3) });

        Assert.Equal([null, null, 3], ordering!.Groups.Select(group => group.SeasonNumber));
    }

    #endregion

    #region Site URLs

    [Fact]
    public void TheSiteUrlsFollowTmdbsPages()
    {
        var season = Mock.Of<ISeason>(entry => entry.ID == TmdbIds.Season(2001) && entry.SeriesID == TmdbIds.Series(1001) && entry.SeasonNumber == 1);
        var episode = Mock.Of<IEpisode>(entry => entry.ID == TmdbIds.Episode(3002) && entry.SeriesID == TmdbIds.Series(1001) && entry.SeasonNumber == 1 && entry.EpisodeNumber == 2);

        Assert.Equal("https://www.themoviedb.org/tv/1001", TmdbSiteUrls.ForEntry(Entry(TmdbIds.Series(1001))));
        Assert.Equal("https://www.themoviedb.org/tv/1001/season/1", TmdbSiteUrls.ForEntry(season));
        Assert.Equal("https://www.themoviedb.org/tv/1001/season/1/episode/2", TmdbSiteUrls.ForEntry(episode));
        Assert.Equal("https://www.themoviedb.org/movie/7001", TmdbSiteUrls.ForEntry(Entry(TmdbIds.Movie(7001))));
        Assert.Equal("https://www.themoviedb.org/collection/8001", TmdbSiteUrls.ForEntry(Entry(TmdbIds.Collection(8001))));
        Assert.Equal("https://www.themoviedb.org/person/5001", TmdbSiteUrls.ForEntry(Entry(TmdbIds.Creator(5001))));
        Assert.Equal("https://www.themoviedb.org/company/21444", TmdbSiteUrls.ForEntry(Entry(TmdbIds.Studio(21444))));
        Assert.Equal("https://www.themoviedb.org/network/98", TmdbSiteUrls.ForEntry(Entry(TmdbIds.Network(98))));
    }

    [Fact]
    public void AnEntryKnownOnlyByItsIDOrAGroupHasNoPage()
    {
        Assert.Null(TmdbSiteUrls.ForEntry(Entry(TmdbIds.Episode(3002))));
        Assert.Null(TmdbSiteUrls.ForEntry(Entry(TmdbIds.OrderingGroup("5acf93efc3a368739a0000a9"))));
        Assert.Null(TmdbSiteUrls.ForEntry(Entry(new(MetadataSource.AniDB, MetadataEntityType.Series, "1"))));
    }

    private static IMetadata Entry(MetadataGuid id)
        => Mock.Of<IMetadata>(entry => entry.ID == id);

    #endregion
}
