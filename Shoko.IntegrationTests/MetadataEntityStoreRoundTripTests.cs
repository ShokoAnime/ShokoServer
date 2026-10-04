using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes through the series, movie and collection stores into the migrated
/// database and reads every table back, so the mappings, the column types and
/// the unique indexes are checked on each backend, along with resource lists
/// longer than a plain string parameter.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataEntityStoreRoundTripTests(DatabaseMigrationFixture fixture)
{
    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_plugin, entityType, id);

    private static TitleStub Title(string value, string languageCode = "en", TitleType type = TitleType.Main)
        => new() { Source = _plugin, Language = TitleLanguage.English, LanguageCode = languageCode, Value = value, Type = type };

    /// <summary>
    /// Throws the caches away and reads every store table again from the
    /// database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        foreach (var repository in new ICachedRepository[]
        {
            services.GetRequiredService<Metadata_SeriesRepository>(),
            services.GetRequiredService<Metadata_SeasonRepository>(),
            services.GetRequiredService<Metadata_EpisodeRepository>(),
            services.GetRequiredService<Metadata_MovieRepository>(),
            services.GetRequiredService<Metadata_CollectionRepository>(),
            services.GetRequiredService<Metadata_Collection_MemberRepository>(),
            services.GetRequiredService<Metadata_TagRepository>(),
            services.GetRequiredService<Metadata_Tag_EntryRepository>(),
            services.GetRequiredService<TextCache>(),
            services.GetRequiredService<Metadata_ContentRatingRepository>(),
        })
            repository.Populate(displayName: false);
    }

    [Fact]
    public void WhatTheEntityStoresWriteReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var movieStore = fixture.Services.GetRequiredService<IMetadataMovieStore>();
        var collectionStore = fixture.Services.GetRequiredService<IMetadataCollectionStore>();
        var tags = fixture.Services.GetRequiredService<IMetadataTagStore>();
        var longText = new string('y', 6000);
        var longID = new string('z', MetadataGuid.MaxIDLength);
        var series = ID(MetadataEntityType.Series, "entity-series-1");

        seriesStore.SaveSeries(new()
        {
            ID = series,
            Titles = [Title("Entity Series"), Title("エンティティ", "ja", TitleType.Official)],
            Overviews = [new TextStub { Source = _plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = longText }],
            Type = AnimeType.Web,
            AirDate = new(2023, 10),
            EndDate = new(2024, 3, 28),
            Rating = 8.25,
            RatingVotes = 1234,
            Restricted = true,
            ReleaseStatus = ReleaseStatus.Finished,
            SourceMaterial = SourceMaterial.Manga,
            OriginalLanguageCode = "ja",
            ProductionCountries = ["JP", " US ", "jp", ""],
            Popularity = 1234.5,
            FavoriteCount = 99,
            Resources = [new() { Type = ResourceType.Website, Name = "Homepage", Url = "https://example.com/series", ID = "series-home" }],
            CrossSourceIDs = [MetadataGuid.Parse("imdb://series/tt0000002"), MetadataGuid.Parse("anidb://series/1")],
            ContentRatings = [new() { CountryCode = "US", Rating = "TV-14" }, new() { CountryCode = "JP", Rating = "R15+", LanguageCode = "ja" }],
            Seasons =
            [
                new() { ID = ID(MetadataEntityType.Season, "entity-season-1"), SeasonNumber = 1, Titles = [Title("Part 1")] },
                new() { ID = ID(MetadataEntityType.Season, longID), SeasonNumber = 2 },
            ],
            Episodes =
            [
                new()
                {
                    ID = ID(MetadataEntityType.Episode, "entity-episode-1"),
                    SeasonID = ID(MetadataEntityType.Season, "entity-season-1"),
                    EpisodeNumber = 1,
                    Rating = 7.5,
                    RatingVotes = 10,
                    Runtime = TimeSpan.FromSeconds(1425),
                    AirDateWithTime = new DateTime(2023, 10, 5, 15, 30, 0, DateTimeKind.Utc),
                    Titles = [Title("Pilot")],
                    Resources = [new() { Type = ResourceType.CrossReference, Name = "IMDb", Url = "https://www.imdb.com/title/tt0000001/", ID = "tt0000001" }],
                    CrossSourceIDs = [MetadataGuid.Parse("imdb://episode/tt0000001")],
                },
                new()
                {
                    ID = ID(MetadataEntityType.Episode, longID),
                    SeasonID = ID(MetadataEntityType.Season, longID),
                    EpisodeNumber = 1,
                    AirDate = new DateOnly(2024, 1, 11),
                },
                new()
                {
                    ID = ID(MetadataEntityType.Episode, "entity-special-1"),
                    SeasonNumber = 0,
                    EpisodeNumber = 1,
                    Type = EpisodeType.Special,
                    AirsBeforeSeasonNumber = 1,
                    AirsBeforeEpisodeNumber = 1,
                    AirsAfterSeasonNumber = 2,
                },
            ],
        });
        tags.SaveTags([new() { ID = ID(MetadataEntityType.Tag, "entity-tag"), Name = "Isekai" }]);
        tags.SetTags(series, [new() { TagID = ID(MetadataEntityType.Tag, "entity-tag"), Weight = 50 }]);

        movieStore.SaveMovie(new()
        {
            ID = ID(MetadataEntityType.Movie, "entity-movie-1"),
            Titles = [Title("Entity Movie")],
            ReleaseDate = new DateOnly(2022, 12, 24),
            Runtime = TimeSpan.FromSeconds(5730.4),
            Video = true,
            OriginalLanguageCode = "ja",
            ProductionCountries = ["JP"],
            Rating = 6.5,
            RatingVotes = 3,
            Resources = [new() { Type = ResourceType.Streaming, Name = "Watch", Url = "https://example.com/watch" }],
            CrossSourceIDs = [MetadataGuid.Parse("imdb://movie/tt0000003")],
            ContentRatings = [new() { CountryCode = "DE", Rating = "FSK 12" }],
        });
        collectionStore.SaveCollection(new()
        {
            ID = ID(MetadataEntityType.Collection, "entity-collection-1"),
            Titles = [Title("Entity Franchise")],
            Members = [ID(MetadataEntityType.Movie, "entity-movie-1"), series],
        });

        Reload();

        var storedSeries = seriesStore.GetSeries(series);
        Assert.NotNull(storedSeries);
        var row = Assert.IsType<Metadata_Series>(storedSeries);
        Assert.Equal(
            (AnimeType.Web, new PartialDateOnly(2023, 10), new PartialDateOnly(2024, 3, 28), 8.25, 1234, true),
            (row.Type, row.AirDate, row.EndDate, row.Rating, row.RatingVotes, row.IsRestricted)
        );
        Assert.Equal((ReleaseStatus.Finished, SourceMaterial.Manga, "ja", 1234.5, 99), (row.ReleaseStatus, row.SourceMaterial, row.OriginalLanguageCode, row.Popularity, row.FavoriteCount));
        Assert.Equal(("https://example.com/series", "series-home"), (Assert.Single(row.Resources).Url, row.Resources[0].ID));
        Assert.Equal(["imdb://series/tt0000002", "anidb://series/1"], storedSeries.CrossSourceIDs.Select(id => id.ToString()));
        Assert.Equal(["JP", "US"], storedSeries.ProductionCountries);
        // A new entry is created when it is first written.
        var createdAt = row.CreatedAt;
        Assert.Equal(row.LastUpdatedAt, createdAt);
        Assert.InRange(createdAt, DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(1));
        Assert.All(storedSeries.Seasons, season => Assert.Equal(createdAt, ((IWithCreationDate)season).CreatedAt));
        Assert.All(storedSeries.Episodes, episode => Assert.Equal(createdAt, ((IWithCreationDate)episode).CreatedAt));
        Assert.Equal(["Entity Series", "エンティティ"], storedSeries.Titles.Select(title => title.Value));
        Assert.Equal([TitleType.Main, TitleType.Official], storedSeries.Titles.Select(title => title.Type));
        Assert.Equal(longText, Assert.Single(storedSeries.Overviews).Value);
        Assert.Equal(["Isekai"], storedSeries.Tags.Select(tag => tag.Name));
        Assert.Equal([("US", "TV-14"), ("JP", "R15+")], storedSeries.ContentRatings.Select(rating => (rating.CountryCode, rating.Value)));
        Assert.Equal("ja", storedSeries.ContentRatings[1].LanguageCode);
        Assert.Equal(["entity-season-1", longID], storedSeries.Seasons.Select(season => season.ID.ID));
        Assert.Equal(3, storedSeries.Episodes.Count);

        var episode = seriesStore.GetEpisode(ID(MetadataEntityType.Episode, "entity-episode-1"));
        Assert.NotNull(episode);
        Assert.Equal((1, 1, 7.5, 10), (episode.SeasonNumber, episode.EpisodeNumber, episode.Rating, episode.RatingVotes));
        Assert.Equal(TimeSpan.FromSeconds(1425), episode.Runtime);
        Assert.Equal(new DateOnly(2023, 10, 5), episode.AirDate);
        Assert.Equal(new DateTime(2023, 10, 5, 15, 30, 0), episode.AirDateWithTime);
        Assert.Equal("Pilot", Assert.Single(episode.Titles).Value);
        var episodeResource = Assert.Single(Assert.IsType<Metadata_Episode>(episode).Resources);
        Assert.Equal((ResourceType.CrossReference, "IMDb", "tt0000001"), (episodeResource.Type, episodeResource.Name, episodeResource.ID));
        Assert.Contains(episode.Resources, resource => resource.ID == "tt0000001");
        Assert.Equal("imdb://episode/tt0000001", Assert.Single(episode.CrossSourceIDs).ToString());
        var longEpisode = seriesStore.GetEpisode(ID(MetadataEntityType.Episode, longID));
        Assert.Equal(ID(MetadataEntityType.Season, longID), longEpisode?.SeasonID);
        Assert.Equal(2, longEpisode?.SeasonNumber);
        Assert.Equal(new DateOnly(2024, 1, 11), longEpisode?.AirDate);
        Assert.Null(longEpisode?.AirDateWithTime);
        var special = Assert.IsType<Metadata_Episode>(seriesStore.GetEpisode(ID(MetadataEntityType.Episode, "entity-special-1")));
        Assert.Equal(EpisodeType.Special, special.Type);
        Assert.Equal(new Metadata_EpisodeExtra { AirsBeforeSeasonNumber = 1, AirsBeforeEpisodeNumber = 1, AirsAfterSeasonNumber = 2 }, special.ExtraData);
        Assert.Null(Assert.IsType<Metadata_Episode>(episode).ExtraData);

        var movie = movieStore.GetMovie(ID(MetadataEntityType.Movie, "entity-movie-1"));
        Assert.NotNull(movie);
        Assert.Equal((new DateTime(2022, 12, 24), true, false, 6.5, 3), (movie.ReleaseDate, movie.Video, movie.Restricted, movie.Rating, movie.RatingVotes));
        Assert.Equal("ja", movie.OriginalLanguageCode);
        Assert.Equal(["JP"], movie.ProductionCountries);
        Assert.Equal(TimeSpan.FromSeconds(5730), movie.Runtime);
        Assert.Equal(((Metadata_Movie)movie).LastUpdatedAt, ((IWithCreationDate)movie).CreatedAt);
        Assert.Equal("Entity Movie", movie.DefaultTitle.Value);
        Assert.Equal("imdb://movie/tt0000003", Assert.Single(movie.CrossSourceIDs).ToString());
        Assert.Equal(("DE", "FSK 12"), movie.ContentRatings.Select(rating => (rating.CountryCode, rating.Value)).Single());
        Assert.Equal(
            [ID(MetadataEntityType.Movie, "entity-movie-1"), series],
            collectionStore.GetMembers(ID(MetadataEntityType.Collection, "entity-collection-1"))
        );
        Assert.Equal("Entity Franchise", collectionStore.GetCollectionsWith(series).Single().DefaultTitle.Value);
        var storedCollection = Assert.IsType<Metadata_Collection>(collectionStore.GetCollectionsWith(series).Single());
        Assert.Equal(storedCollection.LastUpdatedAt, storedCollection.CreatedAt);

        seriesStore.SaveSeries(new()
        {
            ID = series,
            Seasons = [new() { ID = ID(MetadataEntityType.Season, "entity-season-1"), SeasonNumber = 1 }],
            Episodes = [new() { ID = ID(MetadataEntityType.Episode, "entity-episode-1"), SeasonID = ID(MetadataEntityType.Season, "entity-season-1"), EpisodeNumber = 1 }],
        });

        Reload();

        Assert.Equal(["entity-episode-1"], seriesStore.GetSeries(series)!.Episodes.Select(item => item.ID.ID));
        // A later write leaves the creation date alone.
        Assert.Equal(createdAt, Assert.IsType<Metadata_Series>(seriesStore.GetSeries(series)).CreatedAt);
        Assert.Equal(createdAt, ((IWithCreationDate)seriesStore.GetEpisode(ID(MetadataEntityType.Episode, "entity-episode-1"))!).CreatedAt);
        Assert.Null(seriesStore.GetSeason(ID(MetadataEntityType.Season, longID)));
        // Saved without titles, the series is only listed by its synthesized name.
        Assert.True(Assert.Single(seriesStore.GetSeries(series)!.Titles).IsSynthesized);
        Assert.Empty(fixture.Services.GetRequiredService<TextCache>().GetRows(ID(MetadataEntityType.Season, "entity-season-1")));

        Assert.Equal(1, collectionStore.RemoveCollection(ID(MetadataEntityType.Collection, "entity-collection-1")));
        Assert.Equal(1, movieStore.RemoveMovie(ID(MetadataEntityType.Movie, "entity-movie-1")));
        Assert.Equal(3, seriesStore.RemoveSeries(series));
        tags.RemoveTags(series);

        Reload();

        Assert.Null(seriesStore.GetSeries(series));
        Assert.Null(seriesStore.GetEpisode(ID(MetadataEntityType.Episode, "entity-episode-1")));
        Assert.Null(movieStore.GetMovie(ID(MetadataEntityType.Movie, "entity-movie-1")));
        Assert.Empty(fixture.Services.GetRequiredService<Metadata_ContentRatingRepository>().GetByEntry(series));
        Assert.Empty(fixture.Services.GetRequiredService<Metadata_ContentRatingRepository>().GetByEntry(ID(MetadataEntityType.Movie, "entity-movie-1")));
        Assert.Empty(collectionStore.GetCollectionsWith(series));
        Assert.Empty(fixture.Services.GetRequiredService<Metadata_Collection_MemberRepository>().GetAll());
        Assert.Empty(fixture.Services.GetRequiredService<TextCache>().GetRows(series));
    }

    [Fact]
    public void AnEpisodesStoredExtrasLoadWithAPropertyTheServerDoesNotKnow()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var series = ID(MetadataEntityType.Series, "extra-series");
        var special = ID(MetadataEntityType.Episode, "extra-special");
        seriesStore.SaveSeries(new()
        {
            ID = series,
            Episodes = [new() { ID = special, SeasonNumber = 0, EpisodeNumber = 1, Type = EpisodeType.Special, AirsAfterSeasonNumber = 1 }],
        });

        // What a later server could write, with an extra this one has never heard of.
        using (var connection = fixture.OpenConnection())
            Sql.Execute(
                connection,
                """UPDATE Metadata_Episode SET ExtraData = '{"AirsBeforeSeasonNumber":2,"SomeLaterExtra":{"a":[1]}}' WHERE ProviderID = 'extra-special'"""
            );
        Reload();

        var row = Assert.IsType<Metadata_Episode>(seriesStore.GetEpisode(special));
        Assert.Equal(new Metadata_EpisodeExtra { AirsBeforeSeasonNumber = 2 }, row.ExtraData);

        seriesStore.RemoveSeries(series);
    }

    [Fact]
    public void ResourcesLongerThanAPlainStringParameterReadBackWhole()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var movieStore = fixture.Services.GetRequiredService<IMetadataMovieStore>();
        var movie = ID(MetadataEntityType.Movie, "long-resources-movie");

        // Well past the 4000 characters SQL Server's driver gives a plain
        // string parameter, so a cut would leave invalid JSON behind.
        var resources = Enumerable.Range(0, 40)
            .Select(index => new Resource
            {
                Type = ResourceType.Streaming,
                Name = $"Regional streaming page number {index}",
                Url = $"https://streaming.example.com/regional/{index}/series/long-resources-movie?utm_source=shoko&utm_medium=metadata",
                LanguageCode = "ja",
            })
            .ToList();
        movieStore.SaveMovie(new() { ID = movie, Resources = resources });

        Reload();

        var row = Assert.IsType<Metadata_Movie>(movieStore.GetMovie(movie));
        Assert.True(Newtonsoft.Json.JsonConvert.SerializeObject(row.Resources).Length > 4000);
        Assert.Equal(resources.Select(resource => resource.Url), row.Resources.Select(resource => resource.Url));

        movieStore.RemoveMovie(movie);
    }
}
