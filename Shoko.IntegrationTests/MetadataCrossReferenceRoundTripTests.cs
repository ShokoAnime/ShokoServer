using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes links through the cross-reference store into the migrated
/// database and reads them back, so the link columns, the entity kind in a
/// link's identity and the table's unique key are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataCrossReferenceRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const int AnimeID = 990_001;

    private static readonly MetadataSource _plugin = TestSources.Plugin;

    /// <summary>
    /// Throws the caches away and reads the link tables again from the
    /// database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        foreach (var repository in new ICachedRepository[]
        {
            services.GetRequiredService<CrossRef_AniDB_Metadata_SeriesRepository>(),
            services.GetRequiredService<CrossRef_AniDB_Metadata_EpisodeRepository>(),
        })
            repository.Populate(displayName: false);
    }

    [Fact]
    public async Task WhatTheLinksRecordReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var store = fixture.Services.GetRequiredService<IMetadataCrossReferenceStore>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var seasonID = new MetadataGuid(_plugin, MetadataEntityType.Season, new string('s', MetadataGuid.MaxIDLength));
        var film = new MetadataGuid(_plugin, MetadataEntityType.Movie, "film-1");

        await store.MergeSeriesLinks(
            [
                new() { Source = _plugin, AnidbAnimeID = AnimeID, ProviderID = new(_plugin, MetadataEntityType.Series, "show-1") },
                new() { Source = _plugin, AnidbAnimeID = AnimeID, ProviderID = film },
            ],
            cancellationToken: cancellationToken
        );
        await store.MergeEpisodeLinks(
            [
                new()
                {
                    Source = _plugin,
                    AnidbAnimeID = AnimeID,
                    AnidbEpisodeID = 1,
                    ProviderID = new(_plugin, MetadataEntityType.Episode, "ep-1"),
                    ProviderParentID = new(_plugin, MetadataEntityType.Series, "show-1"),
                    SeasonID = seasonID,
                    SeasonNumber = 2,
                    EpisodeNumber = 13,
                },
                new()
                {
                    Source = _plugin,
                    AnidbAnimeID = AnimeID,
                    AnidbEpisodeID = 2,
                    ProviderID = new(_plugin, MetadataEntityType.Episode, "ep-2"),
                },
            ],
            cancellationToken: cancellationToken
        );

        Reload();

        Assert.Equal(
            [new MetadataGuid(_plugin, MetadataEntityType.Series, "show-1"), film],
            store.GetSeriesLinks(AnimeID).Select(link => link.ProviderID)
        );
        Assert.Equal([AnimeID], store.GetLinksTo(film).Select(link => link.AnidbAnimeID));

        var episodes = store.GetEpisodeLinksForSeries(AnimeID);
        Assert.Equal(seasonID, episodes[0].SeasonID);
        Assert.Equal(2, episodes[0].SeasonNumber);
        Assert.Equal(13, episodes[0].EpisodeNumber);
        Assert.Null(episodes[1].SeasonID);
        Assert.Null(episodes[1].SeasonNumber);
        Assert.Null(episodes[1].EpisodeNumber);

        var season = Assert.Single(store.GetAllSeasonLinks(_plugin), link => link.AnidbAnimeID == AnimeID);
        Assert.Equal(seasonID, season.ProviderID);
        Assert.Equal(2, season.SeasonNumber);

        // Nothing is left for the other tests sharing the database.
        await store.RemoveLinksForSeries(_plugin, AnimeID, cancellationToken: cancellationToken);
        Reload();
        Assert.Empty(store.GetSeriesLinks(AnimeID));
        Assert.Empty(store.GetEpisodeLinksForSeries(AnimeID));
    }

    [Fact]
    public async Task ASeriesAndAFilmWithTheSameRawIDAreTwoLinks()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var store = fixture.Services.GetRequiredService<IMetadataCrossReferenceStore>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var series = new MetadataGuid(_plugin, MetadataEntityType.Series, "123");
        var film = new MetadataGuid(_plugin, MetadataEntityType.Movie, "123");

        await store.MergeSeriesLinks([new() { Source = _plugin, AnidbAnimeID = AnimeID + 1, ProviderID = series }], cancellationToken: cancellationToken);
        await store.MergeSeriesLinks([new() { Source = _plugin, AnidbAnimeID = AnimeID + 1, ProviderID = film }], cancellationToken: cancellationToken);

        Reload();

        Assert.Equal([series, film], store.GetSeriesLinks(AnimeID + 1).Select(link => link.ProviderID));
        Assert.Equal([AnimeID + 1], store.GetLinksTo(series).Select(link => link.AnidbAnimeID));
        Assert.Equal([AnimeID + 1], store.GetLinksTo(film).Select(link => link.AnidbAnimeID));

        await store.MergeSeriesLinks([], removals: store.GetLinksTo(film).OfType<IMetadataSeriesCrossReference>(), cancellationToken: cancellationToken);
        Reload();

        Assert.Equal([series], store.GetSeriesLinks(AnimeID + 1).Select(link => link.ProviderID));

        await store.RemoveLinksForSeries(_plugin, AnimeID + 1, cancellationToken: cancellationToken);
        Reload();
        Assert.Empty(store.GetSeriesLinks(AnimeID + 1));
    }

    [Fact]
    public void TheDatabaseRefusesASecondIdenticalSeriesLink()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var source = MetadataNumberRegistry.GetNumber(_plugin);
        var repository = fixture.Services.GetRequiredService<CrossRef_AniDB_Metadata_SeriesRepository>();
        var first = new CrossRef_AniDB_Metadata_Series { Source = _plugin, AnidbAnimeID = AnimeID + 2, ProviderID = "5", Ordering = 0 };
        repository.Save(first);
        try
        {
            // Both a repository save (past the store's slot lock) and a raw insert with the kind
            // left to its default are a series link the anime already has.
            Assert.ThrowsAny<Exception>(() => repository.Save(
                new CrossRef_AniDB_Metadata_Series { Source = _plugin, AnidbAnimeID = AnimeID + 2, ProviderID = "5", Ordering = 1 }
            ));

            using var connection = fixture.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO CrossRef_AniDB_Metadata_Series (Source, AnidbAnimeID, ProviderID, MatchRating, Ordering) " +
                $"VALUES ({source}, {AnimeID + 2}, '5', 0, 2)";
            Assert.ThrowsAny<Exception>(() => command.ExecuteNonQuery());
        }
        finally
        {
            repository.Delete(first);
            Reload();
        }

        Assert.Empty(fixture.Services.GetRequiredService<IMetadataCrossReferenceStore>().GetSeriesLinks(AnimeID + 2));
    }
}
