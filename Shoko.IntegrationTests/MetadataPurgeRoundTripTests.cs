using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.QueueProcessor;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the core's purge job and purge service against the migrated
/// database, so what they remove is read back gone from every table. Also
/// checks that the core's provider job types are registered, closed, for TMDB.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataPurgeRoundTripTests(DatabaseMigrationFixture fixture)
{
    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_plugin, entityType, id);

    /// <summary>
    /// Throws the caches away and reads the store tables again from the
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
            services.GetRequiredService<Metadata_TagRepository>(),
            services.GetRequiredService<Metadata_Tag_EntryRepository>(),
            services.GetRequiredService<TextCache>(),
            services.GetRequiredService<Metadata_CreatorRepository>(),
            services.GetRequiredService<Metadata_CrewRepository>(),
            services.GetRequiredService<Metadata_OrderingRepository>(),
            services.GetRequiredService<Metadata_Ordering_GroupRepository>(),
            services.GetRequiredService<Metadata_Ordering_EntryRepository>(),
        })
            repository.Populate(displayName: false);
    }

    /// <summary>
    /// Runs the core's purge job for one entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>A task that completes once the job has run.</returns>
    private async Task Purge(MetadataGuid entry)
    {
        using var scope = fixture.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PurgeMetadataJob>();
        job.Setup(scope.ServiceProvider);
        job.EntryID = entry.ToString();
        await job.Execute();
    }

    /// <summary>
    /// Gives a series a global ordering of the test source and a user's
    /// ordering, chooses the user's, and hides one of its episodes.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="episode">One of its episodes.</param>
    /// <param name="key">What sets the global ordering's IDs apart.</param>
    /// <returns>The two orderings.</returns>
    private (MetadataGuid Global, MetadataGuid Local) AddOrderings(MetadataGuid series, MetadataGuid episode, string key)
    {
        var orderings = fixture.Services.GetRequiredService<IMetadataOrderingService>();
        var global = orderings.SaveOrdering(new()
        {
            ID = ID(MetadataEntityType.Ordering, $"{key}-ordering"),
            SeriesID = series,
            Name = "DVD Order",
            Type = OrderingType.DVD,
            Groups = [new() { ID = ID(MetadataEntityType.Season, $"{key}-group"), Name = "Disc 1", Episodes = [episode] }],
        });
        var local = orderings.CreateLocalOrdering(new()
        {
            SeriesID = series,
            Name = "Mine",
            Groups = [new() { Name = "All", Episodes = [episode] }],
        });
        Assert.True(orderings.SetPreferredOrdering(series, local.ID));
        Assert.True(orderings.SetEpisodeHidden(episode, true));
        return (global.ID, local.ID);
    }

    /// <summary>
    /// Checks that nothing is left in the ordering tables for a series, and
    /// that neither the choice nor the hidden flag is found any more.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="episode">One of its episodes, which was hidden.</param>
    /// <param name="orderingIDs">The orderings it had.</param>
    private void AssertOrderingsGone(MetadataGuid series, MetadataGuid episode, params MetadataGuid[] orderingIDs)
    {
        var services = fixture.Services;
        Assert.Empty(services.GetRequiredService<Metadata_OrderingRepository>().GetBySeries(series));
        Assert.Null(services.GetRequiredService<IOrderingRowState>().GetPreferredOrdering(series));
        Assert.False(services.GetRequiredService<IOrderingRowState>().IsHidden(episode));
        foreach (var orderingID in orderingIDs)
        {
            Assert.Empty(services.GetRequiredService<Metadata_Ordering_GroupRepository>().GetByOrderingID(orderingID.Source, orderingID.ID));
            Assert.Empty(services.GetRequiredService<Metadata_Ordering_EntryRepository>().GetByOrderingID(orderingID.Source, orderingID.ID));
        }
    }

    [Fact]
    public async Task APurgedSeriesIsGoneFromEveryTable()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var tags = fixture.Services.GetRequiredService<IMetadataTagStore>();
        var series = ID(MetadataEntityType.Series, "purge-series");
        var episode = ID(MetadataEntityType.Episode, "purge-episode");
        seriesStore.SaveSeries(new()
        {
            ID = series,
            Titles = [new TitleStub { Source = _plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = "Purged", Type = TitleType.Main }],
            Seasons = [new() { ID = ID(MetadataEntityType.Season, "purge-season"), SeasonNumber = 1 }],
            Episodes = [new() { ID = episode, SeasonID = ID(MetadataEntityType.Season, "purge-season"), EpisodeNumber = 1 }],
        });
        tags.SaveTags([new() { ID = ID(MetadataEntityType.Tag, "purge-tag"), Name = "Purged" }]);
        tags.SetTags(series, [new() { TagID = ID(MetadataEntityType.Tag, "purge-tag") }]);
        tags.SetTags(episode, [new() { TagID = ID(MetadataEntityType.Tag, "purge-tag") }]);

        await Purge(series);
        Reload();

        Assert.Null(seriesStore.GetSeries(series));
        Assert.Null(seriesStore.GetEpisode(episode));
        Assert.Empty(tags.GetTags(series));
        Assert.Empty(tags.GetTags(episode));
        Assert.Empty(fixture.Services.GetRequiredService<TextCache>().GetRows(series));
        Assert.DoesNotContain(fixture.Services.GetRequiredService<Metadata_Tag_EntryRepository>().GetAll(), row => row.EntryID == series || row.EntryID == episode);
    }

    [Fact]
    public async Task APurgedSeriesTakesEveryOrderingOfItAlong()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var series = ID(MetadataEntityType.Series, "ordered-series");
        var episode = ID(MetadataEntityType.Episode, "ordered-episode");
        var bystander = ID(MetadataEntityType.Series, "ordered-bystander");
        var bystanderEpisode = ID(MetadataEntityType.Episode, "ordered-bystander-episode");
        seriesStore.SaveSeries(new() { ID = series, Episodes = [new() { ID = episode, EpisodeNumber = 1 }] });
        seriesStore.SaveSeries(new() { ID = bystander, Episodes = [new() { ID = bystanderEpisode, EpisodeNumber = 1 }] });
        var (global, local) = AddOrderings(series, episode, "ordered");
        var (keptGlobal, keptLocal) = AddOrderings(bystander, bystanderEpisode, "ordered-bystander");

        await Purge(series);
        Reload();

        AssertOrderingsGone(series, episode, global, local);
        var orderings = fixture.Services.GetRequiredService<IMetadataOrderingService>();
        Assert.NotNull(orderings.GetOrdering(keptGlobal));
        Assert.Equal(keptLocal, orderings.GetPreferredOrdering(seriesStore.GetSeries(bystander)!).ID);
        Assert.True(orderings.IsEpisodeHidden(bystanderEpisode));

        // A series a plugin removes from the store itself takes its orderings too.
        seriesStore.RemoveSeries(bystander);
        Reload();

        AssertOrderingsGone(bystander, bystanderEpisode, keptGlobal, keptLocal);
    }

    [Fact]
    public async Task APurgedTmdbShowTakesEveryOrderingOfItAlong()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var shows = fixture.Services.GetRequiredService<TMDB_ShowRepository>();
        var episodes = fixture.Services.GetRequiredService<TMDB_EpisodeRepository>();
        shows.Save(new TMDB_Show(987_801) { EnglishTitle = "Ordered" });
        episodes.Save(new TMDB_Episode(987_802) { TmdbShowID = 987_801, TmdbSeasonID = 987_803, SeasonNumber = 1, EpisodeNumber = 1 });
        var show = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "987801");
        var episode = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "987802");
        var (global, local) = AddOrderings(show, episode, "tmdb-ordered");

        await Purge(show);
        Reload();

        AssertOrderingsGone(show, episode, global, local);
        Assert.Null(shows.GetByTmdbShowID(987_801));
        Assert.Empty(episodes.GetByTmdbShowID(987_801));
    }

    [Fact]
    public async Task APurgedAnimeAndItsDeletedSeriesTakeEveryOrderingOfThemAlong()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var anime = services.GetRequiredService<AniDB_AnimeRepository>();
        var anidbEpisodes = services.GetRequiredService<AniDB_EpisodeRepository>();
        var shokoSeries = services.GetRequiredService<AnimeSeriesRepository>();
        var shokoEpisodes = services.GetRequiredService<AnimeEpisodeRepository>();
        var orderings = services.GetRequiredService<IMetadataOrderingService>();
        anime.Save(new AniDB_Anime { AnimeID = 987_901, MainTitle = "Ordered" });
        anidbEpisodes.Save(new AniDB_Episode { EpisodeID = 987_902, AnimeID = 987_901, EpisodeNumber = 1, EpisodeType = EpisodeType.Episode });
        var group = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        services.GetRequiredService<AnimeGroupRepository>().Save(group, false);
        var series = new AnimeSeries { AniDB_ID = 987_901, AnimeGroupID = group.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        shokoSeries.Save(series, updateGroups: false, alsoupdateepisodes: false);
        var shokoEpisode = new AnimeEpisode { AnimeSeriesID = series.AnimeSeriesID, AniDB_EpisodeID = 987_902, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        shokoEpisodes.Save(shokoEpisode);
        var anidbSeriesID = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "987901");
        var anidbEpisodeID = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, "987902");
        var shokoSeriesID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, series.AnimeSeriesID.ToString());
        var shokoEpisodeID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, shokoEpisode.AnimeEpisodeID.ToString());
        var (anidbGlobal, anidbLocal) = AddOrderings(anidbSeriesID, anidbEpisodeID, "anidb-ordered");
        var shokoLocal = orderings.CreateLocalOrdering(new()
        {
            SeriesID = shokoSeriesID,
            Name = "Mine",
            Groups = [new() { Name = "All", Episodes = [shokoEpisodeID] }],
        });
        Assert.True(orderings.SetPreferredOrdering(shokoSeriesID, shokoLocal.ID));

        // The choices and the hidden flag are kept on the anime's, the series' and the episode's own rows.
        using (var connection = fixture.OpenConnection())
        {
            Assert.Equal(anidbLocal.ToString(), Scalar(connection, "SELECT PreferredOrderingID FROM AniDB_Anime WHERE AnimeID = 987901"));
            Assert.Equal(1, Convert.ToInt32(Scalar(connection, "SELECT CASE WHEN IsHidden <> 0 THEN 1 ELSE 0 END FROM AniDB_Episode WHERE EpisodeID = 987902")));
            Assert.Equal(shokoLocal.ID.ToString(), Scalar(connection, $"SELECT PreferredOrderingID FROM AnimeSeries WHERE AnimeSeriesID = {series.AnimeSeriesID}"));
        }

        await services.GetRequiredService<IAnidbService>().PurgeAnimeByID(987_901, removeFromMylist: false);
        Reload();

        AssertOrderingsGone(anidbSeriesID, anidbEpisodeID, anidbGlobal, anidbLocal);
        Assert.Empty(services.GetRequiredService<Metadata_OrderingRepository>().GetBySeries(shokoSeriesID));
        Assert.Null(services.GetRequiredService<IOrderingRowState>().GetPreferredOrdering(shokoSeriesID));
        Assert.Null(shokoSeries.GetByAnimeID(987_901));
        Assert.Null(anime.GetByAnimeID(987_901));
    }

    [Fact]
    public async Task AnEpisodeASaveLeavesOutTakesItsTagsAndCreditsButNotItsPeople()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var tags = fixture.Services.GetRequiredService<IMetadataTagStore>();
        var people = fixture.Services.GetRequiredService<IMetadataPeopleStore>();
        var series = ID(MetadataEntityType.Series, "dropped-series");
        var kept = ID(MetadataEntityType.Episode, "kept-episode");
        var dropped = ID(MetadataEntityType.Episode, "dropped-episode");
        var tag = ID(MetadataEntityType.Tag, "dropped-tag");
        var onlyThere = ID(MetadataEntityType.Creator, "dropped-only");
        var shared = ID(MetadataEntityType.Creator, "dropped-shared");
        seriesStore.SaveSeries(new() { ID = series, Episodes = [new() { ID = kept, EpisodeNumber = 1 }, new() { ID = dropped, EpisodeNumber = 2 }] });
        tags.SaveTags([new() { ID = tag, Name = "Dropped" }]);
        tags.SetTags(dropped, [new() { TagID = tag }]);
        people.SaveCreators([new() { ID = onlyThere, Name = "Only" }, new() { ID = shared, Name = "Shared" }]);
        people.SetCrew(dropped, [new() { CreatorID = onlyThere, Name = "Director" }, new() { CreatorID = shared, Name = "Writer" }]);
        people.SetCrew(kept, [new() { CreatorID = shared, Name = "Writer" }]);
        try
        {
            seriesStore.SaveSeries(new() { ID = series, Episodes = [new() { ID = kept, EpisodeNumber = 1 }] });
            Reload();

            Assert.Null(seriesStore.GetEpisode(dropped));
            Assert.Empty(tags.GetTags(dropped));
            Assert.Empty(people.GetCrew(dropped));
            Assert.Single(people.GetCrew(kept));
            Assert.NotNull(people.GetCreator(onlyThere));
        }
        finally
        {
            await Purge(series);
            await fixture.Services.GetRequiredService<IMetadataPurgeService>().PurgeOrphaned(_plugin, DateTime.MaxValue, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TmdbsOrphanedPeopleAndNetworksArePurgedThroughThePurgeServiceByTheCutoff()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var people = fixture.Services.GetRequiredService<TMDB_PersonRepository>();
        var networks = fixture.Services.GetRequiredService<TMDB_NetworkRepository>();
        var purge = fixture.Services.GetRequiredService<IMetadataPurgeService>();
        people.Save(new TMDB_Person(990001) { EnglishName = "Long orphaned", LastOrphanedAt = DateTime.UtcNow.AddDays(-10) });
        people.Save(new TMDB_Person(990002) { EnglishName = "Just orphaned", LastOrphanedAt = DateTime.UtcNow.AddHours(-1) });
        people.Save(new TMDB_Person(990003) { EnglishName = "Never stamped" });
        networks.Save(new TMDB_Network { TmdbNetworkID = 990101, Name = "Long orphaned", LastOrphanedAt = DateTime.UtcNow.AddDays(-10) });
        networks.Save(new TMDB_Network { TmdbNetworkID = 990102, Name = "Just orphaned", LastOrphanedAt = DateTime.UtcNow.AddHours(-1) });

        await purge.PurgeOrphaned(MetadataSource.TMDB, DateTime.Now.AddDays(-7), TestContext.Current.CancellationToken);

        Assert.Null(people.GetByTmdbPersonID(990001));
        Assert.NotNull(people.GetByTmdbPersonID(990002));
        // Found uncredited without a stamp, so stamped now and kept.
        Assert.NotNull(people.GetByTmdbPersonID(990003)?.LastOrphanedAt);
        Assert.Null(networks.GetByTmdbNetworkID(990101));
        Assert.NotNull(networks.GetByTmdbNetworkID(990102));

        people.Delete(people.GetByTmdbPersonID(990002)!);
        people.Delete(people.GetByTmdbPersonID(990003)!);
        networks.Delete(networks.GetByTmdbNetworkID(990102)!);
    }

    [Fact]
    public void TmdbsProviderGetsTheCoresProviderJobTypes()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var jobTypes = fixture.Services.GetRequiredService<QueueJobTypeRegistry>().JobTypes;

        Assert.Contains(typeof(RefreshMetadataJob<TmdbMetadataProvider>), jobTypes);
        Assert.DoesNotContain(jobTypes, type => type.ContainsGenericParameters);
    }
}
