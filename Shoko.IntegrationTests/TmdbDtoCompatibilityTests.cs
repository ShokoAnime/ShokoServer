using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Xunit;
using static Shoko.IntegrationTests.Sql;

using T = Shoko.IntegrationTests.TmdbDataMigrationTests;

namespace Shoko.IntegrationTests;

/// <summary>
/// Builds the APIv3 TMDB models from TMDB's released tables once the copy steps moved them into the
/// shared metadata tables, and compares them with what the models gave from the released tables, the
/// differences the move brings written out one by one.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TmdbDtoCompatibilityTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private const string TmdbImageServer = "https://image.tmdb.org/t/p/original/";

    private static TEnum All<TEnum>() where TEnum : struct, Enum
        => (TEnum)(object)Enum.GetValues<TEnum>().Select(value => Convert.ToInt32(value)).Aggregate(0, (a, b) => a | b);

    private static JObject Golden()
    {
        using var stream = typeof(TmdbDtoCompatibilityTests).Assembly.GetManifestResourceStream("TmdbDtoGolden.json")!;
        using var reader = new JsonTextReader(new StreamReader(stream)) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
    }

    private void Populate()
    {
        foreach (var repository in fixture.Services.GetServices<ICachedRepository>())
            repository.Populate(false, TestContext.Current.CancellationToken);
        fixture.Services.GetRequiredService<TextCache>().Populate(false, TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, object?> Build()
    {
        var show = TmdbCompatibility.GetShow(T.ShowID)!;
        var ordering = TmdbCompatibility.GetAlternateOrdering(T.OrderingID)!;
        var output = new Dictionary<string, object?>
        {
            ["show"] = new TmdbShow(show, All<TmdbShow.IncludeDetails>()),
            ["show-fresh"] = new TmdbShow(TmdbCompatibility.GetShow(T.FreshShowID)!, All<TmdbShow.IncludeDetails>()),
            ["show-ordering"] = new TmdbShow(show, ordering, All<TmdbShow.IncludeDetails>()),
            ["season"] = new TmdbSeason(TmdbCompatibility.GetSeason(T.SeasonID)!, All<TmdbSeason.IncludeDetails>()),
            ["season-specials"] = new TmdbSeason(TmdbCompatibility.GetSeason(T.SpecialsID)!, All<TmdbSeason.IncludeDetails>()),
        };
        foreach (var season in ordering.Seasons.OrderBy(season => season.TmdbEpisodeGroupID))
            output[$"group-{season.TmdbEpisodeGroupID}"] = new TmdbSeason(season, All<TmdbSeason.IncludeDetails>());
        foreach (var episodeID in (int[])[T.FirstEpisodeID, T.SecondEpisodeID, T.SpecialID])
        {
            var episode = TmdbCompatibility.GetEpisode(episodeID)!;
            output[$"episode-{episodeID}"] = new TmdbEpisode(show, episode, All<TmdbEpisode.IncludeDetails>());
            foreach (var place in episode.GetTmdbAlternateOrderingEpisodes().OrderBy(place => place.TmdbEpisodeGroupID))
                output[$"episode-{episodeID}-{place.TmdbEpisodeGroupID}"] = new TmdbEpisode(show, episode, place, All<TmdbEpisode.IncludeDetails>());
        }

        foreach (var movieID in (int[])[T.MovieID, T.MemberMovieID])
        {
            output[$"movie-{movieID}"] = new TmdbMovie(TmdbCompatibility.GetMovie(movieID)!, All<TmdbMovie.IncludeDetails>());
            output[$"search-movie-{movieID}"] = new Search.RemoteSearchMovie(TmdbCompatibility.GetMovie(movieID)!);
        }

        output["collection"] = new TmdbMovie.Collection(TmdbCompatibility.GetCollection(T.CollectionID)!, All<TmdbMovie.Collection.IncludeDetails>());
        output["search-show"] = new Search.RemoteSearchShow(show);
        return output;
    }

    /// <summary>
    /// Puts the seeded times in the zone the test runs in, as the golden file holds them in the zone it
    /// was made in.
    /// </summary>
    private static void UseLocalZone(JObject golden)
    {
        var created = JsonConvert.SerializeObject(new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local).ToUniversalTime()).Trim('"');
        var updated = JsonConvert.SerializeObject(new DateTime(2024, 2, 1, 10, 0, 0, DateTimeKind.Local).ToUniversalTime()).Trim('"');
        foreach (var property in golden.Descendants().OfType<JProperty>().Where(property => property.Name is "CreatedAt" or "LastUpdatedAt").ToList())
            property.Value = property.Value.Value<string>()!.StartsWith("2024-01") ? created : updated;
    }

    /// <summary>
    /// Points the golden file's image addresses at the template TMDB's images use in this run, which
    /// another test may have changed. The TMDB plugin is not loaded here, so the template it would
    /// register is registered when there is none.
    /// </summary>
    private void UseImageTemplate(JObject golden)
    {
        var imageManager = fixture.Services.GetRequiredService<IImageManager>();
        if (imageManager.GetTemplateUrlForSource(MetadataSource.TMDB) is null)
            imageManager.RegisterTemplateUrl(MetadataSource.TMDB, TmdbImageServer + "{0}");
        var template = imageManager.GetTemplateUrlForSource(MetadataSource.TMDB)!;
        var addresses = golden.Descendants().OfType<JValue>().Where(value => value.Value is string text && text.StartsWith(TmdbImageServer, StringComparison.Ordinal));
        foreach (var value in addresses.ToList())
            value.Value = string.Format(template, value.Value<string>()![TmdbImageServer.Length..]);
    }

    /// <summary>
    /// Points the golden file's image IDs at the ones the seeded images have, worked out from their
    /// source and resource ID as every stored image's is.
    /// </summary>
    private static void UseImageIDs(JObject golden)
    {
        foreach (var (localID, resourceID) in T.Posters)
        {
            var seeded = $"00000000-0000-0000-0000-00000{localID}";
            var derived = IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, resourceID).ToString();
            foreach (var value in golden.Descendants().OfType<JValue>().Where(value => value.Value is string text && text == seeded).ToList())
                value.Value = derived;
        }
    }

    #endregion

    #region Documented Differences

    private static IEnumerable<JObject> Entries(JObject golden, string prefix)
        => golden.Properties().Where(property => property.Name.StartsWith(prefix, StringComparison.Ordinal)).Select(property => (JObject)property.Value);

    private static JObject Text(string name, string value, string language, bool isDefault, bool isPreferred, bool isTitle)
        => isTitle
            ? new() { ["Name"] = value, ["Language"] = language, ["Type"] = isDefault ? "Main" : "None", ["Default"] = isDefault, ["Preferred"] = isPreferred, ["Source"] = name }
            : new() { ["Value"] = value, ["Language"] = language, ["Default"] = isDefault, ["Preferred"] = isPreferred, ["Source"] = name };

    private static void Set(JObject golden, string path, object? value)
        => golden.SelectToken(path)!.Replace(value is null ? JValue.CreateNull() : JToken.FromObject(value));

    /// <summary>
    /// Applies to the golden file each difference the move into the shared metadata tables brings.
    /// </summary>
    private static void ApplyDocumentedDifferences(JObject golden)
    {
        // The English title and overview TMDB kept on each row are stored texts now, so they are listed,
        // the title as the default and main one. A generic episode title such as "Episode 1" is not
        // stored but synthesized with the episode's number in the ordering the model is built for, and a
        // season's generic name, such as "Season 1", is not stored but synthesized and listed as its main title.
        foreach (var entry in golden.Properties().Select(property => property.Value).OfType<JObject>().Where(entry => entry.ContainsKey("Titles")))
        {
            var titles = (JArray)entry["Titles"]!;
            foreach (var listed in titles.Where(listed => listed.Value<bool>("Default")))
                listed["Type"] = "Main";
            if (entry.Value<string>("Title") is "Episode 1")
                entry["Title"] = $"Episode {entry.Value<int>("EpisodeNumber")}";
            var title = entry.Value<string>("Title")!;
            if (!titles.Any(other => other.Value<string>("Name") == title))
                titles.Insert(0, Text("TMDB", title, "en-US", true, true, true));
            var overview = entry.Value<string>("Overview");
            if (entry["Overviews"] is JArray overviews && !string.IsNullOrEmpty(overview) && !overviews.Any(other => other.Value<string>("Value") == overview))
                overviews.Insert(0, Text("TMDB", overview, "en-US", false, true, false));
        }

        // A movie's original title is stored in its original language when TMDB did not list it.
        ((JArray)golden["movie-9870141"]!["Titles"]!).Add(Text("TMDB", "映画", "ja", false, false, true));

        // Production countries keep their codes only; the names are the English names of the regions.
        foreach (var show in Entries(golden, "show-ordering").Prepend((JObject)golden["show"]!))
            show["ProductionCountries"]!["KR"] = "South Korea";

        // The counts are worked out from the stored episodes, the hidden ones apart, not read from columns.
        foreach (var show in Entries(golden, "show-ordering").Prepend((JObject)golden["show"]!))
        {
            Set(show, "Ordering[0].HiddenEpisodeCount", 1);
            Set(show, "Ordering[1].EpisodeCount", 2);
            Set(show, "Ordering[1].HiddenEpisodeCount", 1);
            Set(show, "Ordering[1].SeasonCount", 3);
        }

        Set(golden, "show.HiddenEpisodeCount", 1);
        Set(golden, "['show-ordering'].EpisodeCount", 2);
        Set(golden, "['show-ordering'].HiddenEpisodeCount", 1);
        Set(golden, "['show-ordering'].SeasonCount", 3);
        Set(golden, "season.EpisodeCount", 1);
        Set(golden, "season.HiddenEpisodeCount", 1);
        Set(golden, "['group-bb00000000000000000000b1'].HiddenEpisodeCount", 1);
        Set(golden, "['group-bb00000000000000000000b2'].EpisodeCount", 0);

        // The groups of an alternate ordering keep TMDB's numbers, but only the first group numbered 0 is
        // the special group, season 0 for its episodes too: a second one is numbered by its place.
        Set(golden, "['group-bb00000000000000000000b2'].SeasonNumber", 1);
        foreach (var episode in Entries(golden, $"episode-{T.SpecialID}"))
        {
            Set(episode, "Ordering[1].SeasonNumber", 0);
            if (episode.Value<string>("AlternateOrderingID") == T.OrderingID)
                episode["SeasonNumber"] = 0;
        }

        // Content ratings come out by country code, as TMDB's refresh wrote them, whatever order they were stored in.
        foreach (var entry in golden.Properties().Select(property => property.Value).OfType<JObject>().Where(entry => entry["ContentRatings"] is JArray))
            entry["ContentRatings"] = new JArray(((JArray)entry["ContentRatings"]!).OrderBy(rating => rating.Value<string>("Country"), StringComparer.Ordinal));

        // Whether TMDB locked a season or group is not kept, so a season no longer says.
        foreach (var season in Entries(golden, "season").Concat(Entries(golden, "group-")))
            season.Remove("IsLocked");

        // An ID of another source is only listed when there is one; a 0 is no ID.
        Set(golden, "['show-fresh'].TvdbID", null);

        // Genres are tags now, and an entry without any has none, not one empty genre.
        foreach (var key in (string[])["show-fresh", "movie-9870142", "search-movie-9870142"])
            golden[key]!["Genres"] = new JArray();

        // The cast and crew of a season are stored, in the show's original language.
        foreach (var season in Entries(golden, "season"))
            foreach (var role in season["Cast"]!.Concat(season["Crew"]!))
                role["Language"] = "ja";

        // The search model's images come from the stored images; the backdrop TMDB named was never stored.
        Set(golden, "['search-show'].Backdrop", null);

        // A show with dated regular episodes goes by the quarters they aired in, not its first and last dates, so the
        // show stays in Winter; seasons start in whole weeks, so a movie released in early March is Winter.
        foreach (var key in (string[])["show", "show-ordering"])
            ((JArray)golden[key]!["YearlySeasons"]!).RemoveAt(1);
        Set(golden, "['movie-9870141'].YearlySeasons[0].AnimeSeason", "Winter");
    }

    #endregion

    #region Tests

    [Fact]
    public void TheModelsReadTheSameFromTheSharedTables()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        var restored = ReleasedTmdbSchema.Restore(connection, fixture.Backend);
        var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
        try
        {
            T.Seed(connection);

            // The golden file was made from the seed before it gained this link.
            Execute(connection, $"DELETE FROM CrossRef_AniDB_Metadata_Episode WHERE Source = {tmdb} AND AnidbEpisodeID = 9870002");
            Execute(connection, "INSERT INTO CrossRef_AniDB_Metadata_Series (Source, AnidbAnimeID, ProviderID, MatchRating, Ordering, ProviderType) " +
                $"VALUES ({tmdb}, 9870001, '{T.ShowID}', 1, 0, 2)");
            Execute(connection, "INSERT INTO CrossRef_AniDB_Metadata_Episode " +
                "(Source, AnidbAnimeID, AnidbEpisodeID, ProviderID, ProviderParentID, MatchRating, Ordering) " +
                $"VALUES ({tmdb}, 9870001, 9870011, '{T.FirstEpisodeID}', '{T.ShowID}', 1, 0)");
            Execute(connection, "INSERT INTO CrossRef_AniDB_Metadata_Movie (Source, AnidbAnimeID, AnidbEpisodeID, ProviderID, MatchRating, Ordering) " +
                $"VALUES ({tmdb}, 9870002, 9870021, '{T.MovieID}', 1, 0)");
            foreach (var step in T.Steps)
                Run(step, connection);
            Populate();

            var expected = Golden();
            UseLocalZone(expected);
            UseImageTemplate(expected);
            UseImageIDs(expected);
            ApplyDocumentedDifferences(expected);
            var actual = JsonConvert.SerializeObject(Build(), Formatting.Indented);

            Assert.Equal(expected.ToString(Formatting.Indented), actual);
        }
        finally
        {
            Execute(connection, "DELETE FROM CrossRef_AniDB_Metadata_Series WHERE AnidbAnimeID = 9870001");
            Execute(connection, "DELETE FROM CrossRef_AniDB_Metadata_Episode WHERE AnidbAnimeID = 9870001");
            Execute(connection, "DELETE FROM CrossRef_AniDB_Metadata_Movie WHERE AnidbAnimeID = 9870002");
            T.Cleanup(connection, restored, fixture.Backend);
            Populate();
        }
    }

    #endregion
}
