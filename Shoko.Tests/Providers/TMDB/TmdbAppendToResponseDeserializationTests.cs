using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;
using TMDbLib.Utilities.Serializer;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Guards the TMDbLib master upgrade (jellyfin/TMDbLib#626 + System.Text.Json migration).
/// The upgrade keeps the exact same TMDB requests (verified by the reference-library
/// re-scan, tasks T018/T023 — identical request counts and byte-for-byte row parity), so
/// the only new risk is the wire→model binding: STJ presence handling of appended
/// sub-resources and the <c>tvdb_id</c> type change (string → int). These replay the
/// <c>append_to_response</c> payload shapes that <see cref="Shoko.Server.Providers.TMDB.TmdbMetadataService"/>
/// requests and assert each sub-resource Shoko reads still deserializes.
/// </summary>
public class TmdbAppendToResponseDeserializationTests
{
    private static T Deserialize<T>(string json) where T : class
        => TMDbJsonSerializer.Instance.DeserializeFromString<T>(json)
           ?? throw new Xunit.Sdk.XunitException($"Deserialized {typeof(T).Name} was null");

    // Movie: MovieMethods.Translations | ReleaseDates | ExternalIds | Keywords | Credits
    private const string MovieJson = """
    {
      "id": 149,
      "title": "Akira",
      "translations": { "id": 149, "translations": [
        { "iso_639_1": "ja", "iso_3166_1": "JP", "name": "日本語", "english_name": "Japanese",
          "data": { "title": "AKIRA", "overview": "あらすじ" } } ] },
      "release_dates": { "results": [
        { "iso_3166_1": "JP", "release_dates": [ { "certification": "", "type": 3, "release_date": "1988-07-16T00:00:00.000Z" } ] } ] },
      "external_ids": { "imdb_id": "tt0094625", "facebook_id": null, "twitter_id": null },
      "keywords": { "keywords": [ { "id": 970, "name": "anime" }, { "id": 9951, "name": "dystopia" } ] },
      "credits": { "cast": [ { "id": 1, "name": "A" } ], "crew": [ { "id": 2, "name": "B" } ] }
    }
    """;

    // TV show: TvShowMethods.ContentRatings | Translations | ExternalIds | Keywords | EpisodeGroups
    private const string ShowJson = """
    {
      "id": 45782,
      "name": "Sword Art Online",
      "content_ratings": { "results": [ { "iso_3166_1": "US", "rating": "TV-14" } ] },
      "translations": { "id": 45782, "translations": [
        { "iso_639_1": "de", "iso_3166_1": "DE", "name": "Deutsch", "english_name": "German",
          "data": { "name": "Sword Art Online", "overview": "In naher Zukunft..." } } ] },
      "external_ids": { "imdb_id": "tt2250192", "tvdb_id": 259140, "facebook_id": null },
      "keywords": { "results": [ { "id": 6091, "name": "war" } ] },
      "episode_groups": { "results": [
        { "id": "5f8a1d...", "name": "Alternate order", "group_count": 4, "episode_count": 96, "type": 2 } ] }
    }
    """;

    // TV episode: TvEpisodeMethods.ExternalIds | Translations | Credits
    private const string EpisodeJson = """
    {
      "id": 979329,
      "season_number": 1,
      "episode_number": 1,
      "name": "The World of Swords",
      "external_ids": { "imdb_id": "tt2683910", "tvdb_id": 4298471 },
      "translations": { "id": 979329, "translations": [
        { "iso_639_1": "ja", "iso_3166_1": "JP", "name": "", "english_name": "Japanese",
          "data": { "name": "剣の世界", "overview": "" } } ] },
      "credits": { "cast": [ { "id": 1, "name": "A" } ], "crew": [], "guest_stars": [ { "id": 3, "name": "G" } ] }
    }
    """;

    // TV season: TvSeasonMethods.Translations (episode stubs come back on the season body)
    private const string SeasonJson = """
    {
      "id": 61862,
      "season_number": 1,
      "episodes": [
        { "id": 979329, "season_number": 1, "episode_number": 1, "name": "Ep1" },
        { "id": 979330, "season_number": 1, "episode_number": 2, "name": "Ep2" }
      ],
      "translations": { "id": 61862, "translations": [
        { "iso_639_1": "fr", "iso_3166_1": "FR", "name": "Français", "english_name": "French",
          "data": { "name": "Aincrad", "overview": "..." } } ] }
    }
    """;

    [Fact]
    public void Movie_AllAppendedSubResources_Bind()
    {
        var movie = Deserialize<Movie>(MovieJson);

        Assert.Equal("Akira", movie.Title);
        Assert.Equal("ja", Assert.Single(movie.Translations!.Translations!).Iso_639_1);
        Assert.Equal("あらすじ", movie.Translations!.Translations![0].Data!.Overview);
        Assert.Equal("JP", Assert.Single(movie.ReleaseDates!.Results!).Iso_3166_1);
        Assert.Equal("tt0094625", movie.ExternalIds!.ImdbId);
        Assert.Equal(2, movie.Keywords!.Keywords!.Count);
        Assert.Single(movie.Credits!.Cast!);
        Assert.Single(movie.Credits!.Crew!);
    }

    [Fact]
    public void Show_AllAppendedSubResources_Bind()
    {
        var show = Deserialize<TvShow>(ShowJson);

        Assert.Equal("Sword Art Online", show.Name);
        Assert.Equal("TV-14", Assert.Single(show.ContentRatings!.Results!).Rating);
        Assert.Equal("de", Assert.Single(show.Translations!.Translations!).Iso_639_1);
        Assert.Equal("tt2250192", show.ExternalIds!.ImdbId);
        Assert.Single(show.Keywords!.Results!);
        Assert.Equal(4, Assert.Single(show.EpisodeGroups!.Results!).GroupCount);
    }

    [Fact]
    public void Show_TvdbId_DeserializesAsInteger()
    {
        // jellyfin/TMDbLib#626: ExternalIdsTvShow.TvdbId went string? -> int? (TMDB sends it as a
        // JSON number). UpdateShowExternalIDs / UpdateEpisodeExternalIDs rely on this — see task T022.
        var show = Deserialize<TvShow>(ShowJson);
        Assert.Equal(259140, show.ExternalIds!.TvdbId);

        var episode = Deserialize<TvEpisode>(EpisodeJson);
        Assert.Equal(4298471, episode.ExternalIds!.TvdbId);
    }

    [Fact]
    public void Episode_AllAppendedSubResources_Bind()
    {
        var episode = Deserialize<TvEpisode>(EpisodeJson);

        Assert.Equal(1, episode.SeasonNumber);
        Assert.Equal(1, episode.EpisodeNumber);
        Assert.Equal("tt2683910", episode.ExternalIds!.ImdbId);
        Assert.Equal("ja", Assert.Single(episode.Translations!.Translations!).Iso_639_1);
        Assert.Single(episode.Credits!.Cast!);
        Assert.Empty(episode.Credits!.Crew!);
        Assert.Single(episode.Credits!.GuestStars!);
    }

    [Fact]
    public void SearchResults_GenreIds_Bind()
    {
        // TmdbSearchService's restricted auto-match + animation-genre ordering read
        // SearchTv/SearchMovie.GenreIds (task T029). Confirm STJ still binds genre_ids.
        var show = Deserialize<SearchTv>("""{ "id": 45782, "name": "SAO", "genre_ids": [16, 10765] }""");
        Assert.Equal(new[] { 16, 10765 }, show.GenreIds);

        var movie = Deserialize<SearchMovie>("""{ "id": 149, "title": "Akira", "genre_ids": [16, 28, 878] }""");
        Assert.Equal(new[] { 16, 28, 878 }, movie.GenreIds);
    }

    [Fact]
    public void Season_TranslationsAndEpisodeStubs_Bind()
    {
        var season = Deserialize<TvSeason>(SeasonJson);

        Assert.Equal(1, season.SeasonNumber);
        Assert.Equal(2, season.Episodes!.Count);
        Assert.Equal("fr", Assert.Single(season.Translations!.Translations!).Iso_639_1);
    }

    [Theory]
    [InlineData("""{ "id": 149, "title": "Akira" }""")]
    [InlineData("""{ "id": 149, "title": "Akira", "external_ids": null, "keywords": null, "credits": null, "translations": null, "release_dates": null }""")]
    public void Movie_MissingOrNullSubResources_LeaveContainersNull(string json)
    {
        // STJ presence handling: absent and explicit-null must both land as null, not empty
        // containers — TmdbMetadataService's null-guards (task T016) key off exactly this.
        var movie = Deserialize<Movie>(json);

        Assert.Null(movie.Translations);
        Assert.Null(movie.ReleaseDates);
        Assert.Null(movie.ExternalIds);
        Assert.Null(movie.Keywords);
        Assert.Null(movie.Credits);
    }
}
