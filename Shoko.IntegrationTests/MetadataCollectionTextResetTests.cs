using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// A series may read its text off the collection holding its films, so a
/// written or removed collection drops what was worked out from it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataCollectionTextResetTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    /// <summary>
    /// Stores an anime with one normal episode and its Shoko series.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The series and a callback removing everything stored.</returns>
    private (AnimeSeries Series, Action Remove) AddSeries(int animeID)
    {
        var services = fixture.Services;
        var anime = services.GetRequiredService<AniDB_AnimeRepository>();
        var anidbEpisodes = services.GetRequiredService<AniDB_EpisodeRepository>();
        var groups = services.GetRequiredService<AnimeGroupRepository>();
        var seriesRepository = services.GetRequiredService<AnimeSeriesRepository>();
        var anidbAnime = new AniDB_Anime { AnimeID = animeID, MainTitle = "Film Entry", AnimeType = AnimeType.Movie };
        var episode = new AniDB_Episode { EpisodeID = animeID + 1, AnimeID = animeID, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode };
        anime.Save(anidbAnime);
        anidbEpisodes.Save(episode);
        var group = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        groups.Save(group, false);
        var series = new AnimeSeries { AniDB_ID = animeID, AnimeGroupID = group.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        seriesRepository.Save(series, updateGroups: false, alsoupdateepisodes: false);

        return (
            series,
            () =>
            {
                seriesRepository.Delete(series);
                groups.Delete(group);
                anidbEpisodes.Delete(episode);
                anime.Delete(anidbAnime);
            }
        );
    }

    /// <summary>
    /// Works out, and remembers, the entry that speaks for an anime's films on a source, as a
    /// series' titles do, under an entry of its own.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID, whose one episode is the film.</param>
    /// <param name="source">The source.</param>
    /// <returns>Works the entry out, and tells whether it is still remembered.</returns>
    private (Func<IMetadata?> Speaking, Func<bool> Remembered) Probe(int animeID, MetadataSource source)
    {
        var manager = (MetadataTextManager)fixture.Services.GetRequiredService<IMetadataTextManager>();
        var probe = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, $"probe-{animeID}");
        return (
            () => manager.Remember(
                probe,
                TextMemoSlot.PreferredTitle,
                () => manager.SeriesTitleEntry(animeID, AnimeType.Movie, source, () => [(animeID + 1, EpisodeType.Episode, null)]),
                () => null
            ),
            () => manager.TryRemembered<IMetadata?>(probe, TextMemoSlot.PreferredTitle, out _)
        );
    }

    #endregion

    #region Plugin Sources

    [Fact]
    public async Task AWrittenOrRemovedPluginCollection_DropsItsFilmsSeriesText()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var movieStore = fixture.Services.GetRequiredService<IMetadataMovieStore>();
        var collectionStore = fixture.Services.GetRequiredService<IMetadataCollectionStore>();
        var crossReferences = fixture.Services.GetRequiredService<IMetadataCrossReferenceStore>();
        var cancellationToken = TestContext.Current.CancellationToken;
        const int AnimeID = 987_721;
        var film = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Movie, "reset-film");
        var unrelatedFilm = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Movie, "reset-unrelated-film");
        var franchise = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Collection, "reset-franchise");
        var unrelated = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Collection, "reset-unrelated");
        var (_, remove) = AddSeries(AnimeID);
        movieStore.SaveMovie(new() { ID = film });
        await crossReferences.MergeMovieLinks(
            [new() { Source = TestSources.Plugin, AnidbAnimeID = AnimeID, AnidbEpisodeID = AnimeID + 1, ProviderID = film }],
            cancellationToken: cancellationToken
        );

        var (speaking, remembered) = Probe(AnimeID, TestSources.Plugin);

        try
        {
            Assert.Equal(film, speaking()?.ID);
            collectionStore.SaveCollection(new() { ID = unrelated, Members = [unrelatedFilm] });
            Assert.True(remembered());

            collectionStore.SaveCollection(new() { ID = franchise, Members = [film] });
            Assert.False(remembered());

            speaking();
            collectionStore.SaveCollection(new() { ID = franchise, Members = [film] });
            Assert.True(remembered());

            collectionStore.SaveCollection(new() { ID = franchise, Members = [] });
            Assert.False(remembered());

            collectionStore.SaveCollection(new() { ID = franchise, Members = [film] });
            speaking();
            collectionStore.RemoveCollection(franchise);
            Assert.False(remembered());
        }
        finally
        {
            collectionStore.RemoveCollection(franchise);
            collectionStore.RemoveCollection(unrelated);
            await crossReferences.RemoveLinksForSeries(TestSources.Plugin, AnimeID, cancellationToken: cancellationToken);
            movieStore.RemoveMovie(film);
            remove();
        }
    }

    #endregion
}
