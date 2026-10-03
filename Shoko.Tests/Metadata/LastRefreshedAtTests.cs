using System;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers what each source answers for when its entries were last refreshed:
/// the store's column, AniDB's last fetch, and nothing for Shoko's own
/// entries.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class LastRefreshedAtTests
{
    private static readonly DateTime _refreshedAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Local);

    private static readonly DateTime _refreshedAtUtc = _refreshedAt.ToUniversalTime();

    #region Plugin Stores

    [Fact]
    public void AStoredEntryGivesItsColumnInUtc()
    {
        var series = new Metadata_Series { Source = TestSources.Plugin, ProviderID = "1", LastRefreshedAt = _refreshedAt };
        var movie = new Metadata_Movie { Source = TestSources.Plugin, ProviderID = "2", LastRefreshedAt = _refreshedAt };
        var collection = new Metadata_Collection { Source = TestSources.Plugin, ProviderID = "3", LastRefreshedAt = _refreshedAt };
        var creator = new Metadata_Creator { Source = TestSources.Plugin, ProviderID = "4", LastRefreshedAt = _refreshedAt };
        var character = new Metadata_Character { Source = TestSources.Plugin, ProviderID = "5", LastRefreshedAt = _refreshedAt };
        var studio = new Metadata_Studio { Source = TestSources.Plugin, ProviderID = "6", LastRefreshedAt = _refreshedAt };
        var network = new Metadata_Network { Source = TestSources.Plugin, ProviderID = "7", LastRefreshedAt = _refreshedAt };

        Assert.Equal(_refreshedAtUtc, ((ISeries)series).LastRefreshedAt);
        Assert.Equal(_refreshedAtUtc, ((IMovie)movie).LastRefreshedAt);
        Assert.Equal(_refreshedAtUtc, ((ICollection)collection).LastRefreshedAt);
        Assert.Equal(_refreshedAtUtc, ((ICreator)creator).LastRefreshedAt);
        Assert.Equal(_refreshedAtUtc, ((ICharacter)character).LastRefreshedAt);
        Assert.Equal(_refreshedAtUtc, ((IStudio)studio).LastRefreshedAt);
        Assert.Equal(_refreshedAtUtc, ((INetwork)network).LastRefreshedAt);
        Assert.Null(((ISeries)new Metadata_Series { Source = TestSources.Plugin, ProviderID = "8" }).LastRefreshedAt);
    }

    [Fact]
    public void AStoredEpisodeOrSeasonGivesItsSeriesTime()
    {
        var series = new Metadata_Series { Metadata_SeriesID = 1, Source = TestSources.Plugin, ProviderID = "1", LastRefreshedAt = _refreshedAt };
        using var scope = new RepoFactoryScope().With<Metadata_SeriesRepository, int, Metadata_Series>(row => row.Metadata_SeriesID, [series]);

        Assert.Equal(_refreshedAtUtc, ((IEpisode)new Metadata_Episode { Source = TestSources.Plugin, ProviderID = "e1", SeriesID = "1" }).LastRefreshedAt);
        Assert.Equal(_refreshedAtUtc, ((ISeason)new Metadata_Season { Source = TestSources.Plugin, ProviderID = "s1", SeriesID = "1" }).LastRefreshedAt);
        Assert.Null(((IEpisode)new Metadata_Episode { Source = TestSources.Plugin, ProviderID = "e2", SeriesID = "gone" }).LastRefreshedAt);
    }

    [Fact]
    public void AnImplementerThatDoesNotTrackItGivesNoneAndItsEpisodesFollowTheSeries()
    {
        var series = new Mock<ISeries>();
        Assert.Null(new Mock<ISeries> { CallBase = true }.Object.LastRefreshedAt);
        series.Setup(s => s.LastRefreshedAt).Returns(_refreshedAtUtc);
        var episode = new Mock<IEpisode> { CallBase = true };
        episode.Setup(e => e.Series).Returns(series.Object);

        Assert.Equal(_refreshedAtUtc, episode.Object.LastRefreshedAt);
    }

    #endregion

    #region AniDB

    // The column the anime's last fetch is kept in is marked obsolete for writers.
#pragma warning disable CS0618
    private static AniDB_Anime Anime(DateTime descriptionUpdatedAt)
        => new() { AniDB_AnimeID = 1, AnimeID = 10, DateTimeUpdated = _refreshedAt, DateTimeDescUpdated = descriptionUpdatedAt };
#pragma warning restore CS0618

    [Fact]
    public void AnAnidbAnimeGivesItsLastFetchAndItsEpisodesFollowIt()
    {
        var descriptionUpdatedAt = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Local);
        var anime = Anime(descriptionUpdatedAt);
        using var scope = new RepoFactoryScope().With<AniDB_AnimeRepository, int, AniDB_Anime>(row => row.AniDB_AnimeID, [anime]);

        Assert.Equal(_refreshedAtUtc, ((ISeries)anime).LastRefreshedAt);
        Assert.Equal(descriptionUpdatedAt.ToUniversalTime(), ((ISeries)anime).LastUpdatedAt);
        Assert.Equal(_refreshedAtUtc, ((IEpisode)new AniDB_Episode { EpisodeID = 100, AnimeID = 10 }).LastRefreshedAt);
        Assert.Null(((IEpisode)new AniDB_Episode { EpisodeID = 101, AnimeID = 11 }).LastRefreshedAt);
    }

    #endregion

    #region Shoko

    [Fact]
    public void AShokoEntryGivesNoneEvenWhenItsAnimeWasFetched()
    {
        var anime = Anime(_refreshedAt);
        using var scope = new RepoFactoryScope().With<AniDB_AnimeRepository, int, AniDB_Anime>(row => row.AniDB_AnimeID, [anime]);

        Assert.Null(((IShokoSeries)new AnimeSeries { AniDB_ID = 10 }).LastRefreshedAt);
        Assert.Null(((IShokoEpisode)new AnimeEpisode { AniDB_EpisodeID = 100 }).LastRefreshedAt);
        Assert.Null(((IShokoGroup)new AnimeGroup()).LastRefreshedAt);
    }

    #endregion
}
