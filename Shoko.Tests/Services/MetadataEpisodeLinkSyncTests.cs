using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how episode links follow the series store: a link written without
/// its series, season or numbers has them filled from the stored episode, and
/// <see cref="SyncEpisodeLinksJob"/> copies the store's season and numbers
/// onto the links naming its episodes. A link to an episode the store does
/// not hold is left as it was written.
/// </summary>
public class MetadataEpisodeLinkSyncTests
{
    #region Helpers

    private sealed class Tables
    {
        private int _nextID = 100;

        public Mock<CrossRef_AniDB_Metadata_EpisodeRepository> Links { get; }

        public Metadata_EpisodeRepository StoredEpisodes { get; }

        public MetadataCrossReferenceStore Store { get; }

        public Tables(IEnumerable<Metadata_Episode> stored, params CrossRef_AniDB_Metadata_Episode[] links)
        {
            Links = CachedRepo.BuildWritable<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(
                x => x.CrossRef_AniDB_Metadata_EpisodeID,
                links
            );
            var repository = Links.Object;
            Links.Setup(r => r.Save(It.IsAny<CrossRef_AniDB_Metadata_Episode>())).Callback<CrossRef_AniDB_Metadata_Episode>(Keep);
            Links.Setup(r => r.Save(It.IsAny<IReadOnlyCollection<CrossRef_AniDB_Metadata_Episode>>()))
                .Callback<IReadOnlyCollection<CrossRef_AniDB_Metadata_Episode>>(rows =>
                {
                    foreach (var row in rows)
                        Keep(row);
                });
            StoredEpisodes = CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID, stored);
            Store = new(
                CachedRepo.Build<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(x => x.CrossRef_AniDB_Metadata_SeriesID),
                CachedRepo.Build<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(x => x.CrossRef_AniDB_Metadata_MovieID),
                repository,
                StoredEpisodes
            );

            void Keep(CrossRef_AniDB_Metadata_Episode row)
            {
                if (row.CrossRef_AniDB_Metadata_EpisodeID is 0)
                    row.CrossRef_AniDB_Metadata_EpisodeID = _nextID++;
                repository.Cache.Update(row);
            }
        }
    }

    private static Metadata_Episode Stored(int rowID, string id, string seriesID, string? seasonID, int? seasonNumber, int episodeNumber, MetadataSource? source = null)
        => new()
        {
            Metadata_EpisodeID = rowID,
            Source = source ?? TestSources.Plugin,
            ProviderID = id,
            SeriesID = seriesID,
            SeasonID = seasonID,
            SeasonNumber = seasonNumber,
            EpisodeNumber = episodeNumber,
        };

    private static CrossRef_AniDB_Metadata_Episode Link(int rowID, int anidbEpisodeID, string providerID, MetadataSource? source = null)
        => new()
        {
            CrossRef_AniDB_Metadata_EpisodeID = rowID,
            Source = source ?? TestSources.Plugin,
            AnidbAnimeID = 10,
            AnidbEpisodeID = anidbEpisodeID,
            ProviderID = providerID,
        };

    private static MetadataGuid Episode(string id)
        => new(TestSources.Plugin, MetadataEntityType.Episode, id);

    #endregion

    #region Filling

    [Fact]
    public async Task ALinkIsFilledFromTheStoredEpisode()
    {
        var tables = new Tables([Stored(1, "e1", "s1", "s1-2", 2, 5)]);

        await tables.Store.MergeEpisodeLinks(
            [new() { Source = TestSources.Plugin, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = Episode("e1") }],
            cancellationToken: TestContext.Current.CancellationToken
        );

        IMetadataEpisodeCrossReference link = Assert.Single(tables.Links.Object.GetAll());
        Assert.Equal(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "s1"), link.ProviderParentID);
        Assert.Equal(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Season, "s1-2"), link.SeasonID);
        Assert.Equal(2, link.SeasonNumber);
        Assert.Equal(5, link.EpisodeNumber);
    }

    [Fact]
    public async Task WhatALinkGivesIsKept()
    {
        var tables = new Tables([Stored(1, "e1", "s1", "s1-2", 2, 5)]);

        await tables.Store.MergeEpisodeLinks(
            [
                new()
                {
                    Source = TestSources.Plugin,
                    AnidbAnimeID = 10,
                    AnidbEpisodeID = 101,
                    ProviderID = Episode("e1"),
                    SeasonID = new(TestSources.Plugin, MetadataEntityType.Season, "other"),
                    SeasonNumber = 9,
                },
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );

        IMetadataEpisodeCrossReference link = Assert.Single(tables.Links.Object.GetAll());
        Assert.Equal("other", link.SeasonID?.ID);
        Assert.Equal(9, link.SeasonNumber);
        Assert.Equal(5, link.EpisodeNumber);
    }

    [Fact]
    public async Task ALinkToAnEpisodeTheStoreLacksIsLeftAsWritten()
    {
        var tables = new Tables([]);

        await tables.Store.MergeEpisodeLinks(
            [new() { Source = TestSources.Plugin, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = Episode("e1") }],
            cancellationToken: TestContext.Current.CancellationToken
        );

        IMetadataEpisodeCrossReference link = Assert.Single(tables.Links.Object.GetAll());
        Assert.Null(link.ProviderParentID);
        Assert.Null(link.SeasonID);
        Assert.Null(link.SeasonNumber);
        Assert.Null(link.EpisodeNumber);
    }

    [Fact]
    public async Task ACoreSourcesLinkKeepsNoSeasonOrNumbers()
    {
        var tables = new Tables([]);

        await tables.Store.MergeEpisodeLinks(
            [
                new()
                {
                    Source = MetadataSource.TMDB,
                    AnidbAnimeID = 10,
                    AnidbEpisodeID = 101,
                    ProviderID = new(MetadataSource.TMDB, MetadataEntityType.Episode, "71"),
                    ProviderParentID = new(MetadataSource.TMDB, MetadataEntityType.Series, "7"),
                    SeasonID = new(MetadataSource.TMDB, MetadataEntityType.Season, "700"),
                    SeasonNumber = 1,
                    EpisodeNumber = 3,
                },
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var row = Assert.Single(tables.Links.Object.GetAll());
        Assert.Equal("7", row.ProviderParentID);
        Assert.Null(row.ProviderSeasonID);
        Assert.Null(row.SeasonNumber);
        Assert.Null(row.EpisodeNumber);
    }

    #endregion

    #region Syncing

    [Fact]
    public void SyncingASeriesCopiesTheStoresNumbersOntoItsLinks()
    {
        var stale = Link(1, 101, "e1");
        stale.ProviderParentID = "s1";
        stale.ProviderSeasonID = "s1-1";
        stale.SeasonNumber = 1;
        stale.EpisodeNumber = 1;
        var other = Link(2, 201, "x1");
        var tables = new Tables([Stored(1, "e1", "s1", "s1-2", 2, 5), Stored(2, "x1", "s2", null, null, 3)], stale, other);

        var changed = tables.Store.SyncFromSeriesStore(TestSources.Plugin, "s1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([1], changed.Select(link => link.CrossRef_AniDB_Metadata_EpisodeID));
        Assert.Equal(("s1-2", 2, 5), (stale.ProviderSeasonID, stale.SeasonNumber, stale.EpisodeNumber));
        Assert.Null(other.EpisodeNumber);
        Assert.Empty(tables.Store.SyncFromSeriesStore(TestSources.Plugin, "s1", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheSweepCoversEveryPluginSourceAndFillsAMissingSeries()
    {
        var plugin = Link(1, 101, "e1");
        var anilist = Link(2, 102, "a1", TestSources.AniList);
        var tmdb = Link(3, 103, "e1", MetadataSource.TMDB);
        var unknown = Link(4, 104, "missing");
        var tables = new Tables([Stored(1, "e1", "s1", null, null, 7), Stored(2, "a1", "as", "as-1", 1, 2, TestSources.AniList)], plugin, anilist, tmdb, unknown);

        var changed = tables.Store.SyncFromSeriesStore(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], changed.Select(link => link.CrossRef_AniDB_Metadata_EpisodeID).Order());
        Assert.Equal(("s1", null, (int?)null, (int?)7), (plugin.ProviderParentID, plugin.ProviderSeasonID, plugin.SeasonNumber, plugin.EpisodeNumber));
        Assert.Equal(("as-1", 1, 2), (anilist.ProviderSeasonID, anilist.SeasonNumber, anilist.EpisodeNumber));
        Assert.Null(tmdb.EpisodeNumber);
        Assert.Equal(string.Empty, unknown.ProviderParentID);
    }

    [Fact]
    public async Task TheJobSyncsTheSeriesItNames()
    {
        var stale = Link(1, 101, "e1");
        var tables = new Tables([Stored(1, "e1", "s1", "s1-1", 1, 4)], stale);
        var job = new SyncEpisodeLinksJob(tables.Store, Mock.Of<IJobCancellationAccessor>(), Mock.Of<IJobProgressAccessor>())
        {
            Source = TestSources.Plugin.Value,
            SeriesID = "s1",
        };
        job.Setup(new ServiceCollection().AddLogging().BuildServiceProvider());

        await job.Execute();

        Assert.Equal(4, stale.EpisodeNumber);
    }

    [Fact]
    public async Task TheJobSyncsNothingForTextThatIsNoSource()
    {
        var stale = Link(1, 101, "e1");
        var tables = new Tables([Stored(1, "e1", "s1", "s1-1", 1, 4)], stale);
        var job = new SyncEpisodeLinksJob(tables.Store, Mock.Of<IJobCancellationAccessor>(), Mock.Of<IJobProgressAccessor>()) { Source = "Not a source!" };
        job.Setup(new ServiceCollection().AddLogging().BuildServiceProvider());

        await job.Execute();

        Assert.Null(stale.EpisodeNumber);
    }

    #endregion
}
