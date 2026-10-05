using System;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Plugin.Tmdb.Mapping;
using Xunit;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   Refreshes shows, movies and collections from the fixtures through the
///   plugin's refresh service and checks what reaches the stores.
/// </summary>
public sealed class TmdbRefreshServiceTests : IDisposable
{
    private static readonly MetadataGuid _show = TmdbIds.Series(1001);

    private readonly TmdbServiceHarness _harness = new();

    public void Dispose()
        => _harness.Dispose();

    #region Shows

    [Fact]
    public async Task AShowIsStoredWithItsSeasonsAndEpisodes()
    {
        _harness.RouteShow();

        Assert.True(await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken));

        var series = _harness.StoreData.Series[_show];
        Assert.Equal(PartialDateOnly.FromDateOnly(new DateOnly(2023, 9, 29)), series.AirDate);
        Assert.Equal(PartialDateOnly.FromDateOnly(new DateOnly(2023, 10, 6)), series.EndDate);
        Assert.Equal(ReleaseStatus.Finished, series.ReleaseStatus);
        Assert.Equal("ja", series.OriginalLanguageCode);
        Assert.Equal(["JP"], series.ProductionCountries);
        Assert.Equal(["https://example.com/journey"], series.Resources.Select(resource => resource.Url));
        Assert.Equal([TmdbIds.Season(2000), TmdbIds.Season(2001)], series.Seasons.Select(season => season.ID));
        Assert.Equal([TmdbIds.Episode(3000), TmdbIds.Episode(3001), TmdbIds.Episode(3002)], series.Episodes.Select(episode => episode.ID));

        var special = series.Episodes[0];
        Assert.Equal(EpisodeType.Special, special.Type);
        Assert.Equal(TmdbIds.Season(2000), special.SeasonID);
        Assert.Equal(TimeSpan.FromMinutes(12), special.Runtime);
        var first = series.Episodes[1];
        Assert.Equal(EpisodeType.Episode, first.Type);
        Assert.Equal(new DateOnly(2023, 9, 29), first.AirDate);
        Assert.Equal(["imdb://episode/tt30000001", "tvdb://episode/9000001"], first.CrossSourceIDs.Select(id => id.ToString()));

        // The images TMDb names on each entry are its defaults.
        Assert.Equal(
            [(ImageEntityType.Primary, "show-poster.jpg"), (ImageEntityType.Backdrop, "show-backdrop.jpg")],
            series.DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value))
        );
        Assert.Equal([(ImageEntityType.Primary, "season-1.jpg")], series.Seasons[1].DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value)));
        Assert.Equal([(ImageEntityType.Backdrop, "still-3001.jpg")], first.DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value)));
    }

    [Fact]
    public async Task TheShowsTextsFollowTheLanguageOrder()
    {
        _harness.RouteShow();
        _harness.StoreData.LanguageOrder.Clear();
        _harness.StoreData.LanguageOrder.AddRange([TitleLanguage.German, TitleLanguage.Romaji]);

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        var series = _harness.StoreData.Series[_show];
        // English as the main title, the original, the German translation and the romaji transcription;
        // the French one repeats the English and is not stored again.
        Assert.Equal(
            [("en", TitleType.Main, "Journey's End"), ("ja", TitleType.Official, "旅の終わり"), ("de", TitleType.Official, "Das Ende der Reise"), ("x-jat", TitleType.Synonym, "Tabi no Owari")],
            series.Titles.Select(title => (title.LanguageCode, title.Type, title.Value))
        );
        Assert.Equal(["en", "de"], series.Overviews.Select(overview => overview.LanguageCode));

        // Generic episode titles are passed on as they are; the core drops them.
        Assert.Equal(["Episode 1"], series.Episodes[0].Titles.Select(title => title.Value));
        Assert.Equal(["en", "de"], series.Episodes[1].Titles.Select(title => title.LanguageCode));
        Assert.Equal(["Episode 2"], series.Episodes[2].Titles.Select(title => title.Value));
    }

    [Fact]
    public async Task EveryTranslationIsKeptWhenAllAreDownloaded()
    {
        _harness.RouteShow();
        _harness.Configuration.DownloadAllTitles = true;
        _harness.Configuration.DownloadAllContentRatings = true;
        _harness.StoreData.LanguageOrder.Clear();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        var series = _harness.StoreData.Series[_show];
        Assert.Contains(series.Titles, title => title.LanguageCode is "de");
        Assert.Equal(["US", "JP", "BR"], series.ContentRatings.Select(rating => rating.CountryCode));
    }

    [Fact]
    public async Task OnlyTheContentRatingsOfKeptLanguagesAreStored()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        Assert.Equal([("US", "TV-14"), ("JP", "G")], _harness.StoreData.Series[_show].ContentRatings.Select(rating => (rating.CountryCode, rating.Rating)));
    }

    [Fact]
    public async Task GenresAndKeywordsAreTagsByTheirTmdbIDs_AJoinedGenreOnePerPart()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [TmdbIds.Genre(16), TmdbIds.GenrePart(10759, 1), TmdbIds.GenrePart(10759, 2), TmdbIds.Keyword(210024), TmdbIds.Keyword(9663)],
            _harness.StoreData.EntryTags[_show].Select(tag => tag.TagID)
        );
        var adventure = _harness.StoreData.Tags[TmdbIds.GenrePart(10759, 2)];
        Assert.Equal(("Adventure", TagKind.Genre), (adventure.Name, adventure.Kind));
        Assert.Equal(TagKind.Keyword, _harness.StoreData.Tags[TmdbIds.Keyword(210024)].Kind);
    }

    [Fact]
    public async Task StudiosNetworksAndSuggestionsAreStored()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        var studio = _harness.StoreData.Studios[TmdbIds.Studio(21444)];
        Assert.Equal(("MADHOUSE", "JP"), (studio.Name, studio.CountryOfOrigin));
        Assert.Equal([TmdbIds.Studio(21444)], _harness.StoreData.EntryStudios[_show].Select(entry => entry.StudioID));
        Assert.Equal("JP", _harness.StoreData.Networks[TmdbIds.Network(98)].CountryOfOrigin);
        Assert.Equal([TmdbIds.Network(98)], _harness.StoreData.EntryNetworks[_show].Select(entry => entry.NetworkID));
        // The logos the show lists are their defaults.
        Assert.Equal([(ImageEntityType.Primary, "company-21444.png")], studio.DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value)));
        Assert.Equal([(ImageEntityType.Primary, "network-98.png")], _harness.StoreData.Networks[TmdbIds.Network(98)].DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value)));

        // The show itself, listed as similar to itself, is left out.
        Assert.Equal(
            [(TmdbIds.Series(1002), SuggestionKind.Recommended, 0), (TmdbIds.Series(1003), SuggestionKind.Similar, 0)],
            _harness.StoreData.Suggestions[_show].Select(suggestion => (suggestion.SuggestedID, suggestion.Kind, suggestion.Order ?? -1))
        );
    }

    [Fact]
    public async Task TheEpisodesCreditsAreStoredWithTheNamesTheStubsNeed()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        var cast = _harness.StoreData.Cast[TmdbIds.Episode(3001)];
        Assert.Equal(
            [("Frieren", "Atsumi Tanezaki", null), ("Fern", "Kana Ichinose", null), ("Heiter", "Hiroaki Hirata", TmdbCredits.GuestStarNotes)],
            cast.Select(credit => (credit.Name, credit.CreatorName, credit.RoleNotes))
        );
        Assert.All(cast, credit => Assert.Equal("ja", credit.LanguageCode));
        Assert.Equal(
            [("Directing, Director", CrewRoleType.Director), ("Sound, Original Music Composer", CrewRoleType.Music)],
            _harness.StoreData.Crew[TmdbIds.Episode(3001)].Select(credit => (credit.Name, credit.RoleType))
        );
    }

    [Fact]
    public async Task TheSeasonAndShowCreditsAreWorkedOutFromTheEpisodes()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        // Each role once, the regular roles by their best place, the guest after.
        Assert.Equal(["Frieren", "Fern", "Heiter"], _harness.StoreData.Cast[TmdbIds.Season(2001)].Select(credit => credit.Name));
        Assert.Equal(["Frieren"], _harness.StoreData.Cast[TmdbIds.Season(2000)].Select(credit => credit.Name));
        Assert.Equal(["Frieren", "Fern", "Heiter"], _harness.StoreData.Cast[_show].Select(credit => credit.Name));
        Assert.Equal(
            [TmdbIds.Creator(6001), TmdbIds.Creator(6002)],
            _harness.StoreData.Crew[_show].Select(credit => credit.CreatorID)
        );
    }

    [Fact]
    public async Task NoCreditsAreFetchedOnAQuickRefresh()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);

        Assert.Empty(_harness.StoreData.Cast);
        Assert.DoesNotContain(_harness.Routes.Requests, request => request.Query.Contains("credits", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheEpisodeGroupIsStoredAsAnOrderingWithItsNetwork()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        var ordering = Assert.Single(_harness.StoreData.Orderings.Values);
        Assert.Equal(TmdbIds.Ordering("5acf93e60e0a26346d0000ce"), ordering.ID);
        Assert.Equal(_show, ordering.SeriesID);
        Assert.Equal(OrderingType.Production, ordering.Type);
        Assert.Equal([TmdbIds.Network(98)], ordering.Networks);
        Assert.Equal([("Specials", true), ("Part 1", false)], ordering.Groups.Select(group => (Assert.Single(group.Titles).Value, group.IsSpecial)));
        Assert.All(
            ordering.Groups.Select(group => group.Titles[0]),
            title => Assert.Equal(("en", "US", TitleType.Main), (title.LanguageCode, title.CountryCode, title.Type))
        );

        // In the group's order, without the episode the show does not have.
        Assert.Equal([TmdbIds.Episode(3002), TmdbIds.Episode(3001)], ordering.Groups[1].Episodes);
    }

    [Fact]
    public async Task AnOrderingTmdbNoLongerHasIsRemoved()
    {
        _harness.RouteShow();
        var gone = TmdbIds.Ordering("gone");
        _harness.StoreData.Orderings[gone] = new() { ID = gone, SeriesID = _show };

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        Assert.Equal([gone], _harness.StoreData.RemovedOrderings);
    }

    [Fact]
    public async Task AQuickRefreshFetchesNoEpisodeOnItsOwn()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);

        Assert.Equal(3, _harness.StoreData.Series[_show].Episodes.Count);
        Assert.DoesNotContain(_harness.Routes.Paths, path => path.Contains("/episode/", StringComparison.Ordinal));
        Assert.Empty(_harness.StoreData.Orderings);
        _harness.Linking.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnlyWhatChangedIsFetchedAgain()
    {
        _harness.RouteShow();
        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);
        _harness.Routes.Fixture("tv/1001/changes", "changes-show-1001.json");
        var before = _harness.Routes.Paths.Count;
        var keptTitles = _harness.StoreData.Series[_show].Episodes[1].Titles;

        await _harness.Refresh.RefreshShow(1001, new() { LastRefreshedAt = DateTime.UtcNow.AddHours(-1) }, TestContext.Current.CancellationToken);

        var asked = _harness.Routes.Paths.Skip(before).ToList();
        Assert.DoesNotContain("tv/1001/season/0", asked);
        Assert.DoesNotContain("tv/1001/season/1/episode/1", asked);
        Assert.Contains("tv/1001/season/1/episode/2", asked);

        // What was kept is written again as it was, credits and texts included.
        var series = _harness.StoreData.Series[_show];
        Assert.Equal(3, series.Episodes.Count);
        Assert.Same(keptTitles, series.Episodes[1].Titles);
        Assert.Equal(["Frieren", "Fern", "Heiter"], _harness.StoreData.Cast[_show].Select(credit => credit.Name));
    }

    [Fact]
    public async Task AShowTmdbDoesNotHaveIsLeftAsItWas()
    {
        Assert.False(await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken));
        Assert.Empty(_harness.StoreData.Series);
    }

    [Fact]
    public async Task WithoutAnApiKeyTheProviderIsNotConfigured()
    {
        _harness.Configuration.UserApiKey = null;

        await Assert.ThrowsAsync<MetadataProviderNotConfiguredException>(() => _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken));
    }

    #endregion

    #region Movies & Collections

    [Fact]
    public async Task AMovieIsStoredWithItsCreditsAndNamesItsCollectionWithoutFetchingIt()
    {
        _harness.RouteMovie();

        Assert.True(await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken));

        var movie = _harness.StoreData.Movies[TmdbIds.Movie(7001)];
        // The first production country's theatrical release.
        Assert.Equal(new DateOnly(2025, 1, 10), movie.ReleaseDate);
        Assert.Equal(TimeSpan.FromMinutes(106), movie.Runtime);
        Assert.Equal(["JP", "US"], movie.ProductionCountries);
        // Every certification of a country once, in TMDb's order.
        Assert.Equal([("US", "PG"), ("JP", "G"), ("JP", "PG12")], movie.ContentRatings.Select(rating => (rating.CountryCode, rating.Rating)));
        Assert.Equal(["imdb://movie/tt40000001"], movie.CrossSourceIDs.Select(id => id.ToString()));
        Assert.Equal(("en", TitleType.Main, "Journey's End: The Movie"), (movie.Titles[0].LanguageCode, movie.Titles[0].Type, movie.Titles[0].Value));

        // In TMDb's billing order.
        Assert.Equal(["Frieren", "Fern"], _harness.StoreData.Cast[TmdbIds.Movie(7001)].Select(credit => credit.Name));
        Assert.Equal([TmdbIds.Genre(16), TmdbIds.Genre(14), TmdbIds.Keyword(210024)], _harness.StoreData.EntryTags[TmdbIds.Movie(7001)].Select(tag => tag.TagID));

        // The core fetches the collection the movie names.
        Assert.Equal(TmdbIds.Collection(8001), movie.CollectionID);
        Assert.Empty(_harness.StoreData.Collections);
        Assert.Equal(0, _harness.Routes.Count("collection/8001"));
    }

    [Fact]
    public async Task ACollectionIsStoredWithItsMovies()
    {
        _harness.RouteMovie();

        Assert.True(await _harness.Refresh.RefreshCollection(8001, TestContext.Current.CancellationToken));

        var collection = _harness.StoreData.Collections[TmdbIds.Collection(8001)];
        Assert.Equal([TmdbIds.Movie(7001), TmdbIds.Movie(7002)], collection.Members);
        Assert.Equal("Journey's End Collection", collection.Titles[0].Value);
        Assert.Equal([(ImageEntityType.Primary, "collection-poster.jpg")], collection.DefaultImageResourceIDs!.Select(pair => (pair.Key, pair.Value)));
    }

    [Fact]
    public async Task AMovieThatDidNotChangeIsNotFetched()
    {
        _harness.RouteMovie();
        await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken);
        _harness.Routes.Json("movie/7001/changes", """{"changes":[]}""");

        Assert.False(await _harness.Refresh.RefreshMovie(7001, new() { LastRefreshedAt = DateTime.UtcNow.AddHours(-1) }, TestContext.Current.CancellationToken));

        Assert.Equal(1, _harness.Routes.Count("movie/7001"));
        Assert.Equal(1, _harness.StoreData.Saves["Movies"]);
    }

    [Fact]
    public async Task AMovieLastRefreshedOutsideTheChangesWindowIsFetched()
    {
        _harness.RouteMovie();
        await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken);

        Assert.True(await _harness.Refresh.RefreshMovie(7001, new() { LastRefreshedAt = DateTime.UtcNow.AddDays(-30) }, TestContext.Current.CancellationToken));

        Assert.Equal(0, _harness.Routes.Count("movie/7001/changes"));
    }

    [Fact]
    public async Task ACollectionTmdbNoLongerHasIsRemoved()
    {
        _harness.RouteMovie();
        await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken);

        Assert.False(await _harness.Refresh.RefreshCollection(8002, TestContext.Current.CancellationToken));
        Assert.Empty(_harness.StoreData.RemovedCollections);

        _harness.StoreData.Collections[TmdbIds.Collection(8002)] = new() { ID = TmdbIds.Collection(8002) };
        Assert.False(await _harness.Refresh.RefreshCollection(8002, TestContext.Current.CancellationToken));
        Assert.Equal([TmdbIds.Collection(8002)], _harness.StoreData.RemovedCollections);
    }

    #endregion
}
