using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

using AnidbTextListing = Shoko.Server.Providers.AniDB.AnidbTextListing;
using ListedTitle = Shoko.Server.Providers.AniDB.AnidbTextListing.ListedTitle;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers how AniDB's anime and episode titles are stored through the text store, read back
/// through AniDB's models as they always were, and how an episode's generic title is left out
/// and made up again when read.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbTextTests
{
    #region Helpers

    private static MetadataGuid AnimeID(int id)
        => new(MetadataSource.AniDB, MetadataEntityType.Series, id.ToString());

    private static MetadataGuid EpisodeID(int id)
        => new(MetadataSource.AniDB, MetadataEntityType.Episode, id.ToString());

    private static ListedTitle Listed(TitleLanguage language, TitleType type, string value)
        => new(language, type, value);

    private static TitleStub Stored(TitleLanguage language, string languageCode, TitleType type, string value)
        => new() { Source = MetadataSource.AniDB, Language = language, LanguageCode = languageCode, Type = type, Value = value };

    private static string[] Values(IEnumerable<IText> titles)
        => [.. titles.Select(title => title.Value)];

    /// <summary>
    /// A text store over an in-memory cache, with a text manager over it put in place for the
    /// models, and the AniDB episodes installed.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public MetadataTextStore Store { get; } = new(new TextCache(), new CacheOnlyRowWriter());

        public World(params AniDB_Episode[] episodes)
            => _scope = new RepoFactoryScope()
                .Set(TestTextManager.Build(Store))
                .Set(Store.Cache)
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, episodes);

        public void Dispose()
            => _scope.Dispose();
    }

    #endregion

    #region Planning

    [Fact]
    public void AFirstImportStoresTheAnimeTitlesInAniDBsOrderWithoutRepeatsOrEmptyOnes()
    {
        var titles = AnidbTextListing.PlanAnimeTitles(
            [],
            [
                Listed(TitleLanguage.Romaji, TitleType.Main, "Main"),
                Listed(TitleLanguage.Japanese, TitleType.Official, "日本語"),
                Listed(TitleLanguage.Romaji, TitleType.Main, "Main"),
                Listed(TitleLanguage.English, TitleType.Synonym, string.Empty),
                Listed(TitleLanguage.English, TitleType.Official, "English"),
            ]
        );

        Assert.Equal(["Main", "日本語", "English"], Values(titles));
        Assert.Equal(["x-jat", "ja", "en"], titles.Select(title => title.LanguageCode));
    }

    [Fact]
    public void ARefreshStoresTheTitlesInAniDBsOrderKeepingTheStoredCodes()
    {
        IReadOnlyList<ITitle> stored =
        [
            Stored(TitleLanguage.English, "EN", TitleType.Official, "English"),
            Stored(TitleLanguage.Romaji, "x-jat", TitleType.Main, "Main"),
            Stored(TitleLanguage.German, "de", TitleType.Official, "Deutsch"),
        ];

        var titles = AnidbTextListing.PlanAnimeTitles(
            stored,
            [
                Listed(TitleLanguage.Romaji, TitleType.Main, "Main"),
                Listed(TitleLanguage.French, TitleType.Official, "Français"),
                Listed(TitleLanguage.English, TitleType.Official, "English"),
            ]
        );

        Assert.Equal(["Main", "Français", "English"], Values(titles));
        Assert.Equal(["x-jat", "fr", "EN"], titles.Select(title => title.LanguageCode));
    }

    [Theory]
    [InlineData(EpisodeType.Episode, 5, "Episode 5", false)]
    [InlineData(EpisodeType.Episode, 1, "Episode 17", true)]
    [InlineData(EpisodeType.Special, 1, "Episode S1", false)]
    [InlineData(EpisodeType.Special, 1, "Episode 1", true)]
    [InlineData(EpisodeType.Episode, 3, "The Real Title", true)]
    public void AnEpisodesGenericTitleWithItsOwnNumberIsNotStored(EpisodeType type, int number, string value, bool stored)
    {
        var titles = AnidbTextListing.PlanEpisodeTitles([], [Listed(TitleLanguage.English, TitleType.None, value)], type, number);

        Assert.Equal(stored, titles.Count is 1);
    }

    [Fact]
    public void AnEpisodesEnglishTitlesAreItsMainOnes()
    {
        var titles = AnidbTextListing.PlanEpisodeTitles(
            [],
            [Listed(TitleLanguage.Japanese, TitleType.None, "日本語"), Listed(TitleLanguage.English, TitleType.None, "English")],
            EpisodeType.Episode,
            1
        );

        Assert.Equal([TitleType.None, TitleType.Main], titles.Select(title => title.Type));
    }

    [Fact]
    public void ParsedEpisodeTitlesAreStoredWithoutTheGenericOne()
    {
        const string Xml = """
            <anime id="1" restricted="false">
              <type>TV Series</type>
              <episodecount>1</episodecount>
              <titles><title xml:lang="x-jat" type="main">Main Title</title></titles>
              <episodes>
                <episode id="1001" update="2020-01-05">
                  <epno>2</epno>
                  <length>24</length>
                  <title xml:lang="en">Episode 2</title>
                  <title xml:lang="ja">本当の題名</title>
                  <title xml:lang="fr">Épisode 2</title>
                </episode>
              </episodes>
            </anime>
            """;
        var response = new HttpAnimeParser(NullLogger<HttpAnimeParser>.Instance).Parse(1, Xml)!;
        var episode = Assert.Single(response.Episodes);

        var titles = AnidbTextListing.PlanEpisodeTitles(
            [],
            episode.Titles.Select(title => Listed(title.Language, TitleType.None, title.Title)),
            (EpisodeType)episode.EpisodeType,
            episode.EpisodeNumber
        );

        Assert.Equal(["本当の題名"], Values(titles));
    }

    #endregion

    #region Reading

    [Fact]
    public void AnAnimesTitlesAreReadInTheStoredOrderWithTheCodeOfTheirLanguage()
    {
        using var world = new World();
        world.Store.SetTitles(AnimeID(1), MetadataSource.AniDB,
        [
            Stored(TitleLanguage.Romaji, "X-JAT", TitleType.Main, "Main"),
            Stored(TitleLanguage.English, "EN", TitleType.Official, "English"),
        ]);
        var anime = new AniDB_Anime { AnimeID = 1, MainTitle = "Main" };

        Assert.Equal(["Main", "English"], Values(anime.Titles));
        Assert.Equal(["x-jat", "en"], anime.Titles.Select(title => title.LanguageCode));
        Assert.Equal("Main|English", anime.AllTitles);
        Assert.Equal("Main", anime.DefaultTitle.Value);
        Assert.Equal("Main", anime.Title);
        Assert.Equal(["X-JAT", "EN"], world.Store.GetTitles(AnimeID(1)).Select(title => title.LanguageCode));
    }

    [Fact]
    public void AnAnimesChoiceFollowsAWriteToItsTitles()
    {
        using var world = new World();
        world.Store.SetTitles(AnimeID(1), MetadataSource.AniDB, [Stored(TitleLanguage.Romaji, "x-jat", TitleType.Main, "Old")]);
        var anime = new AniDB_Anime { AnimeID = 1 };
        Assert.Equal("Old", anime.Title);

        world.Store.SetTitles(AnimeID(1), MetadataSource.AniDB, [Stored(TitleLanguage.Romaji, "x-jat", TitleType.Main, "New")]);

        Assert.Equal("New", anime.Title);
    }

    [Fact]
    public void AnEpisodesTitlesComeFromTheStoreAndItsEnglishOneIsTheDefault()
    {
        using var world = new World(new AniDB_Episode { AniDB_EpisodeID = 7, EpisodeID = 100, AnimeID = 1, EpisodeNumber = 4 });
        world.Store.SetTitles(EpisodeID(100), MetadataSource.AniDB,
        [
            Stored(TitleLanguage.Japanese, "JA", TitleType.None, "日本語"),
            Stored(TitleLanguage.English, "en", TitleType.Main, "English"),
        ]);
        var episode = new AniDB_Episode { AniDB_EpisodeID = 7, EpisodeID = 100, AnimeID = 1, EpisodeNumber = 4 };

        Assert.Equal(["日本語", "English"], Values(episode.GetTitles()));
        Assert.Equal(["ja", "en"], episode.GetTitles().Select(title => title.LanguageCode));
        Assert.Equal(["日本語"], Values(episode.GetTitles(TitleLanguage.Japanese)));
        Assert.Equal("English", episode.DefaultTitle.Value);
        Assert.Equal("English", episode.EnglishTitle);
        Assert.False(episode.DefaultTitle.IsSynthesized);
    }

    [Fact]
    public void ADescriptionOnTheRowIsListedAsTheDefaultKeptThere()
    {
        var anime = (IWithOverviews)new AniDB_Anime { AnimeID = 1, Description = "Told by AniDB." };
        var episode = (IWithOverviews)new AniDB_Episode { EpisodeID = 100, Description = "Told by AniDB." };
        var character = (IWithOverviews)new AniDB_Character { CharacterID = 5, Description = "Told by AniDB." };
        var empty = (IWithOverviews)new AniDB_Anime { AnimeID = 2 };

        Assert.All([anime, episode, character], entry => Assert.True(Assert.Single(entry.Overviews).IsInlineDefault));
        Assert.False(Assert.Single(empty.Overviews).IsInlineDefault);
    }

    [Fact]
    public void AnEpisodeWithoutAnEnglishTitleIsNamedByAniDBsGenericOne()
    {
        using var world = new World();
        world.Store.SetTitles(EpisodeID(100), MetadataSource.AniDB, [Stored(TitleLanguage.Japanese, "ja", TitleType.None, "日本語")]);
        var episode = new AniDB_Episode { EpisodeID = 100, EpisodeType = EpisodeType.Special, EpisodeNumber = 2 };

        Assert.Equal("Episode S2", episode.EnglishTitle);
        Assert.True(episode.DefaultTitle.IsSynthesized);
        Assert.Equal(["日本語"], Values(((IWithTitles)episode).Titles));
    }

    #endregion

    #region Tags

    [Fact]
    public void ATagsNameIsTheCoresOverallTitleOfIt()
    {
        using var world = new World();
        var tag = new AniDB_Tag { TagID = 2797, TagNameSource = "new" };
        var tagID = ((IMetadata)tag).ID;
        Assert.Equal("new", tag.TagName);

        world.Store.SetOverallTitle(tagID, MetadataSource.Shoko,
            new TitleStub { Source = MetadataSource.Shoko, Language = TitleLanguage.English, LanguageCode = "en", Value = "original work", Type = TitleType.Main });

        Assert.Equal("original work", tag.TagName);
        Assert.Equal(TextPreference.Overall, Assert.Single(world.Store.GetTitles(tagID, MetadataSource.Shoko)).Preference);

        world.Store.SetOverallTitle(tagID, MetadataSource.Shoko, null);

        Assert.Equal("new", tag.TagName);
        Assert.Null(tag.TagNameOverride);
    }

    #endregion

    #region Writing

    [Fact]
    public void StoringTheSameTitlesAgainChangesNothing()
    {
        using var world = new World();
        var anime = new AniDB_Anime { AnimeID = 1 };
        List<ResponseTitle> titles =
        [
            new() { Language = TitleLanguage.Romaji, TitleType = TitleType.Main, Title = "Main" },
            new() { Language = TitleLanguage.English, TitleType = TitleType.Official, Title = "English" },
        ];
        IReadOnlyList<(MetadataGuid, IReadOnlyList<ITitle>)> episodes = [(EpisodeID(100), [Stored(TitleLanguage.English, "en", TitleType.Main, "One")])];

        Assert.True(AnimeCreator.StoreTitles(world.Store, titles, anime, episodes, []));
        var ids = world.Store.GetTitles(AnimeID(1)).Select(title => title.ID).ToList();

        Assert.False(AnimeCreator.StoreTitles(world.Store, titles, anime, episodes, []));
        Assert.Equal(ids, world.Store.GetTitles(AnimeID(1)).Select(title => title.ID));
        Assert.Equal(["One"], Values(world.Store.GetTitles(EpisodeID(100))));
    }

    [Fact]
    public void AReimportWithAReorderedListingReordersTheStoredTitles()
    {
        const string FirstXml = """
            <anime id="1" restricted="false">
              <type>TV Series</type>
              <episodecount>1</episodecount>
              <titles>
                <title xml:lang="x-jat" type="main">Main Title</title>
                <title xml:lang="ja" type="official">日本語</title>
                <title xml:lang="en" type="official">English</title>
              </titles>
              <episodes>
                <episode id="100" update="2020-01-05">
                  <epno>1</epno>
                  <length>24</length>
                  <title xml:lang="ja">第一話</title>
                  <title xml:lang="en">The First</title>
                </episode>
              </episodes>
            </anime>
            """;
        const string SecondXml = """
            <anime id="1" restricted="false">
              <type>TV Series</type>
              <episodecount>1</episodecount>
              <titles>
                <title xml:lang="en" type="official">English</title>
                <title xml:lang="de" type="official">Deutsch</title>
                <title xml:lang="x-jat" type="main">Main Title</title>
                <title xml:lang="ja" type="official">日本語</title>
              </titles>
              <episodes>
                <episode id="100" update="2020-01-06">
                  <epno>1</epno>
                  <length>24</length>
                  <title xml:lang="x-jat">Dai Ichi Wa</title>
                  <title xml:lang="en">The First</title>
                  <title xml:lang="ja">第一話</title>
                </episode>
              </episodes>
            </anime>
            """;
        using var world = new World();
        var anime = new AniDB_Anime { AnimeID = 1 };
        var parser = new HttpAnimeParser(NullLogger<HttpAnimeParser>.Instance);
        void Import(string xml)
        {
            var response = parser.Parse(1, xml)!;
            var episode = Assert.Single(response.Episodes);
            var episodeTitles = AnidbTextListing.PlanEpisodeTitles(
                world.Store.GetTitles(EpisodeID(100), MetadataSource.AniDB),
                episode.Titles.Select(title => Listed(title.Language, TitleType.None, title.Title)),
                (EpisodeType)episode.EpisodeType,
                episode.EpisodeNumber
            );
            AnimeCreator.StoreTitles(world.Store, response.Titles, anime, [(EpisodeID(100), episodeTitles)], []);
        }

        Import(FirstXml);
        var ids = world.Store.GetTitles(AnimeID(1)).ToDictionary(title => title.Value, title => title.ID);
        Assert.Equal(["Main Title", "日本語", "English"], Values(world.Store.GetTitles(AnimeID(1))));
        Assert.Equal(["第一話", "The First"], Values(world.Store.GetTitles(EpisodeID(100))));

        Import(SecondXml);

        var titles = world.Store.GetTitles(AnimeID(1));
        Assert.Equal(["English", "Deutsch", "Main Title", "日本語"], Values(titles));
        Assert.Equal([0, 1, 2, 3], titles.Select(title => title.Ordering));
        Assert.All(titles.Where(title => ids.ContainsKey(title.Value)), title => Assert.Equal(ids[title.Value], title.ID));
        Assert.Equal("English|Deutsch|Main Title|日本語", anime.AllTitles);
        Assert.Equal(["Dai Ichi Wa", "The First", "第一話"], Values(world.Store.GetTitles(EpisodeID(100))));
        Assert.Equal([0, 1, 2], world.Store.GetTitles(EpisodeID(100)).Select(title => title.Ordering));
    }

    [Fact]
    public void StoringTitlesRemovesTheTextsOfTheEpisodesThatWent()
    {
        using var world = new World();
        var anime = new AniDB_Anime { AnimeID = 1 };
        world.Store.SetTitles(EpisodeID(100), MetadataSource.AniDB, [Stored(TitleLanguage.English, "en", TitleType.Main, "Gone")]);

        AnimeCreator.StoreTitles(world.Store, null, anime, [], [EpisodeID(100)]);

        Assert.Empty(world.Store.GetTitles(EpisodeID(100)));
    }

    #endregion

    #region Searching

    [Fact]
    public void TheTitleSearchMatchesMainAndOfficialTitlesAndFollowsWrites()
    {
        using var world = new World();
        var search = new AnidbTitleSearch(world.Store.Cache);
        world.Store.SetTitles(AnimeID(1), MetadataSource.AniDB,
        [
            Stored(TitleLanguage.Romaji, "x-jat", TitleType.Main, "Ｍａｉｎ Title"),
            Stored(TitleLanguage.English, "en", TitleType.Synonym, "Synonym"),
        ]);

        Assert.True(search.AnimeMatchesSearch(1, AnidbTitleSearch.NormalizeForSearch("MAIN")));
        Assert.False(search.AnimeMatchesSearch(1, AnidbTitleSearch.NormalizeForSearch("synonym")));

        world.Store.SetTitles(AnimeID(1), MetadataSource.AniDB, [Stored(TitleLanguage.Romaji, "x-jat", TitleType.Main, "Renamed")]);

        Assert.False(search.AnimeMatchesSearch(1, "main"));
        Assert.True(search.AnimeMatchesSearch(1, "renamed"));
    }

    #endregion
}
