using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the series, movie and collection stores against in-memory tables:
/// an entry is stored whole and read back whole, saving it again replaces
/// what was stored and says how much changed, a bad write changes nothing,
/// and every write raises the matching events.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataEntityStoreTests
{
    #region Helpers

    private sealed class Tables
    {
        public CacheOnlyRowWriter Writer { get; } = new();

        public Metadata_SeriesRepository Series { get; }
            = CachedRepo.Build<Metadata_SeriesRepository, int, Metadata_Series>(row => row.Metadata_SeriesID);

        public Metadata_SeasonRepository Seasons { get; }
            = CachedRepo.Build<Metadata_SeasonRepository, int, Metadata_Season>(row => row.Metadata_SeasonID);

        public Metadata_EpisodeRepository Episodes { get; }
            = CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID);

        public Metadata_MovieRepository Movies { get; }
            = CachedRepo.Build<Metadata_MovieRepository, int, Metadata_Movie>(row => row.Metadata_MovieID);

        public Metadata_CollectionRepository Collections { get; }
            = CachedRepo.Build<Metadata_CollectionRepository, int, Metadata_Collection>(row => row.Metadata_CollectionID);

        public Metadata_Collection_MemberRepository Members { get; }
            = CachedRepo.Build<Metadata_Collection_MemberRepository, int, Metadata_Collection_Member>(row => row.Metadata_Collection_MemberID);

        public TextCache Texts { get; } = new();

        public MetadataTextStore TextStore { get; }

        public MetadataTextManager TextManager { get; }

        public Tables()
        {
            TextStore = new(Texts, Writer);
            TextManager = TestTextManager.Build(TextStore);
        }

        public Metadata_ContentRatingRepository ContentRatings { get; }
            = CachedRepo.Build<Metadata_ContentRatingRepository, int, Metadata_ContentRating>(row => row.Metadata_ContentRatingID);

        public Mock<IQueueScheduler> Scheduler { get; } = new();

        public Mock<IMetadataCrossReferenceStore> Links { get; } = NoLinks.Build();

        public OrderingTables Orderings { get; } = new();

        /// <summary>
        /// The ordering service the last series store built removes orderings
        /// through, which finds the series and episodes in that store.
        /// </summary>
        public MetadataOrderingService? OrderingService { get; private set; }

        /// <summary>
        /// The chosen orderings and hidden flags on the last series store's
        /// rows.
        /// </summary>
        public OrderingRowState? RowState { get; private set; }

        public MetadataSeriesStore SeriesStore()
        {
            MetadataSeriesStore? store = null;
            var metadata = new Mock<IMetadataService>();
            metadata.Setup(m => m.GetSeries(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => store!.GetSeries(id));
            metadata.Setup(m => m.GetEpisode(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => store!.GetEpisode(id));
            var rowState = RowState = new OrderingRowState(
                CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID),
                CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AnimeID),
                CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.EpisodeID),
                CachedRepo.Build<TMDB_ShowRepository, int, TMDB_Show>(show => show.TmdbShowID),
                CachedRepo.Build<TMDB_EpisodeRepository, int, TMDB_Episode>(episode => episode.TmdbEpisodeID),
                new Lazy<MetadataSeriesStore>(() => store!)
            );
            var orderingService = OrderingService = Orderings.Build(() => metadata.Object, rowState);
            store = new(
                Series,
                Seasons,
                Episodes,
                ContentRatings,
                TextStore,
                NoCleanup.Build(),
                new Lazy<MetadataOrderingService>(() => orderingService),
                new Lazy<IMetadataCrossReferenceStore>(() => Links.Object),
                Scheduler.Object,
                NullLogger<MetadataSeriesStore>.Instance
            );
            return store;
        }

        public MetadataMovieStore MovieStore() => new(Movies, ContentRatings, TextStore, NoCleanup.Build());

        public MetadataCollectionStore CollectionStore() => new(
            Collections,
            Members,
            TextStore,
            NoCleanup.Build()
        );

        public RepoFactoryScope Scope()
            => new RepoFactoryScope().Set(Series).Set(Seasons).Set(Episodes).Set(Movies).Set(Collections).Set(Members).Set(Texts).Set(ContentRatings).Set(TextManager);
    }

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(TestSources.Plugin, entityType, id);

    private static TitleStub Title(string value, TitleType type = TitleType.Main)
        => new() { Source = TestSources.Plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = value, Type = type };

    private static MetadataSeriesData Series(string id, params MetadataEpisodeData[] episodes)
        => new()
        {
            ID = ID(MetadataEntityType.Series, id),
            Titles = [Title($"Series {id}")],
            Overviews = [new TextStub { Source = TestSources.Plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = "About it." }],
            Type = AnimeType.TV,
            AirDate = new PartialDateOnly(2024, 4),
            Rating = 7.5,
            RatingVotes = 30,
            Seasons =
            [
                new() { ID = ID(MetadataEntityType.Season, $"{id}-1"), SeasonNumber = 1, Titles = [Title("Season 1")] },
                new() { ID = ID(MetadataEntityType.Season, $"{id}-2"), SeasonNumber = 2 },
            ],
            Episodes = episodes,
        };

    private static MetadataEpisodeData Episode(string id, string? season, int number, string? title = null)
        => new()
        {
            ID = ID(MetadataEntityType.Episode, id),
            SeasonID = season is null ? null : ID(MetadataEntityType.Season, season),
            EpisodeNumber = number,
            Titles = title is null ? [] : [Title(title)],
        };

    /// <summary>
    /// Collects the events the stores raise for one series or movie, since
    /// the event handler is shared by every test.
    /// </summary>
    private sealed class Events : IDisposable
    {
        private readonly MetadataGuid _entry;

        public List<SeriesInfoUpdatedEventArgs> Series { get; } = [];

        public List<SeasonInfoUpdatedEventArgs> Seasons { get; } = [];

        public List<MovieInfoUpdatedEventArgs> Movies { get; } = [];

        public Events(MetadataGuid entry)
        {
            _entry = entry;
            ShokoEventHandler.Instance.SeriesUpdated += OnSeries;
            ShokoEventHandler.Instance.SeasonUpdated += OnSeason;
            ShokoEventHandler.Instance.MovieUpdated += OnMovie;
        }

        private void OnSeries(object? sender, SeriesInfoUpdatedEventArgs args)
        {
            if (args.SeriesInfo.ID == _entry)
                Series.Add(args);
        }

        private void OnSeason(object? sender, SeasonInfoUpdatedEventArgs args)
        {
            if (args.SeriesInfo.ID == _entry)
                Seasons.Add(args);
        }

        private void OnMovie(object? sender, MovieInfoUpdatedEventArgs args)
        {
            if (args.MovieInfo.ID == _entry)
                Movies.Add(args);
        }

        public void Dispose()
        {
            ShokoEventHandler.Instance.SeriesUpdated -= OnSeries;
            ShokoEventHandler.Instance.SeasonUpdated -= OnSeason;
            ShokoEventHandler.Instance.MovieUpdated -= OnMovie;
        }
    }

    #endregion

    #region Series

    [Fact]
    public void ASeriesReadsBackWithItsSeasonsAndEpisodes()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();

        var changed = store.SaveSeries(Series("s1",
            Episode("e1", "s1-1", 1, "First"),
            Episode("e2", "s1-1", 2) with { AirDateWithTime = new DateTime(2024, 4, 7, 23, 30, 0, DateTimeKind.Utc), Runtime = TimeSpan.FromMinutes(24) },
            Episode("e3", "s1-2", 1),
            Episode("sp", null, 1) with { Type = EpisodeType.Special, SeasonNumber = 0 }
        ));

        Assert.Equal(7, changed);
        var series = Assert.IsType<Metadata_Series>(store.GetSeries(ID(MetadataEntityType.Series, "s1")));
        Assert.Equal(AnimeType.TV, series.Type);
        Assert.Equal(new PartialDateOnly(2024, 4), series.AirDate);
        Assert.Equal(["s1-1", "s1-2"], ((ISeries)series).Seasons.Select(season => season.ID.ID));
        Assert.Equal(["sp", "e1", "e2", "e3"], ((ISeries)series).Episodes.Select(episode => episode.ID.ID));
        Assert.Equal(3, ((ISeries)series).EpisodeCounts.Episodes);
        Assert.Equal(1, ((ISeries)series).EpisodeCounts.Specials);

        var episode = store.GetEpisode(ID(MetadataEntityType.Episode, "e2"))!;
        Assert.Equal(ID(MetadataEntityType.Series, "s1"), episode.SeriesID);
        Assert.Equal(ID(MetadataEntityType.Season, "s1-1"), episode.SeasonID);
        Assert.Equal(1, episode.SeasonNumber);
        Assert.Equal(2, episode.EpisodeNumber);
        Assert.Equal(new DateOnly(2024, 4, 7), episode.AirDate);
        Assert.Same(series, episode.Series);
        Assert.Null(store.GetEpisode(ID(MetadataEntityType.Episode, "sp"))!.SeasonID);

        var season = store.GetSeason(ID(MetadataEntityType.Season, "s1-1"))!;
        Assert.Equal(["Season 1"], season.Titles.Select(title => title.Value));
        Assert.Equal(["e1", "e2"], season.Episodes.Select(item => item.ID.ID));
        Assert.Same(series, season.Series);
    }

    [Fact]
    public void SavingASeriesAgainReplacesWhatWasStored()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1, "First"), Episode("e2", "s1-2", 1, "Second")));
        var keptID = tables.Episodes.GetByProviderID(TestSources.Plugin, "e1")!.Metadata_EpisodeID;

        var changed = store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1, "First, again")) with
        {
            Seasons = [new() { ID = ID(MetadataEntityType.Season, "s1-1"), SeasonNumber = 1, Titles = [Title("Season 1")] }],
        });

        // The first episode's title changed; the second season and episode went.
        Assert.Equal(3, changed);
        Assert.Equal(keptID, tables.Episodes.GetByProviderID(TestSources.Plugin, "e1")!.Metadata_EpisodeID);
        Assert.Null(store.GetEpisode(ID(MetadataEntityType.Episode, "e2")));
        Assert.Null(store.GetSeason(ID(MetadataEntityType.Season, "s1-2")));
        Assert.Empty(tables.Texts.GetRows(ID(MetadataEntityType.Episode, "e2")));
        Assert.Equal(["First, again"], store.GetEpisode(ID(MetadataEntityType.Episode, "e1"))!.Titles.Select(title => title.Value));
    }

    [Fact]
    public void DroppingAnEpisodeRemovesTheLinksNamingItAndNoOthers()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        var dropped = ID(MetadataEntityType.Episode, "e2");
        var link = new CrossRef_AniDB_Metadata_Episode { Source = TestSources.Plugin, AnidbAnimeID = 1, AnidbEpisodeID = 2, ProviderID = "e2" };
        tables.Links.Setup(links => links.GetLinksTo(dropped)).Returns([link]);
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1), Episode("e2", "s1-1", 2)));

        // Saving it unchanged drops nothing.
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1), Episode("e2", "s1-1", 2)));
        tables.Links.Verify(
            links => links.MergeEpisodeLinks(It.IsAny<IEnumerable<MetadataEpisodeLinkData>>(), It.IsAny<IEnumerable<IMetadataEpisodeCrossReference>?>(), null, default),
            Times.Never
        );

        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1)));

        tables.Links.Verify(links => links.MergeEpisodeLinks(
            It.Is<IEnumerable<MetadataEpisodeLinkData>>(added => !added.Any()),
            It.Is<IEnumerable<IMetadataEpisodeCrossReference>?>(removed => removed!.Single() == link),
            null,
            default
        ), Times.Once);
        tables.Links.Verify(links => links.GetLinksTo(ID(MetadataEntityType.Episode, "e1")), Times.Never);
    }

    [Fact]
    public void SavingTheSameSeriesTwiceChangesNothing()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        var data = Series("s1", Episode("e1", "s1-1", 1, "First"));
        store.SaveSeries(data);
        var writtenAt = tables.Series.GetByProviderID(TestSources.Plugin, "s1")!.LastUpdatedAt;

        using var events = new Events(data.ID);
        Assert.Equal(0, store.SaveSeries(data));

        Assert.Equal(writtenAt, tables.Series.GetByProviderID(TestSources.Plugin, "s1")!.LastUpdatedAt);
        Assert.Empty(events.Series);
    }

    [Fact]
    public void AnEpisodesLinksAreStoredAndAChangeToThemUpdatesIt()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        static Resource Link(string id) => new() { Type = ResourceType.CrossReference, Name = "IMDb", Url = "https://www.imdb.com/title/tt0000001/", ID = id };
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1) with { Resources = [Link("tt0000001")] }));

        var row = tables.Episodes.GetByProviderID(TestSources.Plugin, "e1")!;
        Assert.Equal(("tt0000001", "https://www.imdb.com/title/tt0000001/"), (Assert.Single(row.Resources).ID, row.Resources[0].Url));
        Assert.Equal(0, store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1) with { Resources = [Link("tt0000001")] })));

        // Only the ID differs, which is still a change to the episode.
        Assert.Equal(1, store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1) with { Resources = [Link("tt0000002")] })));
        Assert.Equal("tt0000002", Assert.Single(tables.Episodes.GetByProviderID(TestSources.Plugin, "e1")!.Resources).ID);
    }

    [Fact]
    public void TheIDsOtherSourcesGaveASeriesAndItsEpisodesAreStoredOnceEach()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        var imdb = MetadataGuid.Parse("imdb://series/tt0000001");
        var other = ID(MetadataEntityType.Series, "elsewhere");
        var episodeID = MetadataGuid.Parse("imdb://episode/tt0000002");
        MetadataSeriesData Data(params MetadataGuid[] ids)
            => Series("s1", Episode("e1", "s1-1", 1) with { CrossSourceIDs = [episodeID] }) with { CrossSourceIDs = ids };

        store.SaveSeries(Data(imdb, other, imdb));

        var series = store.GetSeries(ID(MetadataEntityType.Series, "s1"))!;
        Assert.Equal([imdb, other], series.CrossSourceIDs);
        Assert.Equal([other], series.GetCrossSourceIDs(TestSources.Plugin));
        Assert.Equal([episodeID], store.GetEpisode(ID(MetadataEntityType.Episode, "e1"))!.CrossSourceIDs);
        Assert.Equal(0, store.SaveSeries(Data(imdb, other)));

        // The order is the source's, so a new order is a change.
        Assert.Equal(1, store.SaveSeries(Data(other, imdb)));
        Assert.Equal([other, imdb], store.GetSeries(ID(MetadataEntityType.Series, "s1"))!.CrossSourceIDs);
        Assert.Throws<ArgumentNullException>(() => store.SaveSeries(Data(imdb, null!)));
        Assert.Equal([other, imdb], store.GetSeries(ID(MetadataEntityType.Series, "s1"))!.CrossSourceIDs);
    }

    [Fact]
    public void AnEpisodeInASeasonTheSeriesLacksIsRefusedAndNothingIsWritten()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();

        var exception = Assert.Throws<ArgumentException>(() => store.SaveSeries(Series("s1", Episode("e1", "s9-9", 1))));

        Assert.Contains("s9-9", exception.Message);
        Assert.Empty(tables.Series.GetAll());
        Assert.Empty(tables.Episodes.GetAll());
        Assert.Empty(tables.Texts.GetAll());
    }

    [Fact]
    public void AnEpisodeStoredUnderAnotherSeriesMovesToTheOneSaved()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1)));

        store.SaveSeries(Series("s2", Episode("e1", "s2-1", 5)));

        var episode = store.GetEpisode(ID(MetadataEntityType.Episode, "e1"))!;
        Assert.Equal(ID(MetadataEntityType.Series, "s2"), episode.SeriesID);
        Assert.Equal(5, episode.EpisodeNumber);
        Assert.Empty(store.GetSeries(ID(MetadataEntityType.Series, "s1"))!.Episodes);
        Assert.Single(tables.Episodes.GetAll());
    }

    [Fact]
    public void WritingASeriesRaisesItsEvents()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        var data = Series("events", Episode("ev1", "events-1", 1));
        using var events = new Events(data.ID);

        store.SaveSeries(data);
        store.SaveSeries(data with { Rating = 9, Episodes = [Episode("ev1", "events-1", 1), Episode("ev2", "events-2", 1)] });
        store.RemoveSeries(data.ID);

        Assert.Equal([UpdateReason.Added, UpdateReason.Updated, UpdateReason.Removed], events.Series.Select(args => args.Reason));
        Assert.Equal([UpdateReason.Added], events.Series[0].Episodes.Select(args => args.Reason).Distinct());
        Assert.Equal(["ev2"], events.Series[1].Episodes.Select(args => args.EpisodeInfo.ID.ID));
        Assert.Equal(2, events.Series[2].Episodes.Count);
        Assert.Equal(2, events.Series[2].Seasons.Count);
        Assert.Equal(
            [UpdateReason.Added, UpdateReason.Added, UpdateReason.Removed, UpdateReason.Removed],
            events.Seasons.Select(args => args.Reason)
        );
    }

    [Fact]
    public void ChangingASeriesEpisodesQueuesTheSyncOfTheirLinks()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var queued = new List<SyncEpisodeLinksJob>();
        tables.Scheduler
            .Setup(scheduler => scheduler.Enqueue(It.IsAny<Action<SyncEpisodeLinksJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Callback<Action<SyncEpisodeLinksJob>?, bool, DateTimeOffset?, CancellationToken>((configure, _, _, _) =>
            {
                var job = new SyncEpisodeLinksJob(null!);
                configure?.Invoke(job);
                queued.Add(job);
            })
            .Returns(Task.CompletedTask);
        var store = tables.SeriesStore();
        var data = Series("s1", Episode("e1", "s1-1", 1));

        store.SaveSeries(data);
        store.SaveSeries(data);
        store.SaveSeries(data with { Rating = 1 });
        store.SaveSeries(data with { Episodes = [Episode("e1", "s1-1", 2)] });

        // Only the writes that added or changed an episode queue a sync.
        Assert.Equal(2, queued.Count);
        Assert.All(queued, job => Assert.Equal((TestSources.Plugin.Value, "s1"), (job.Source, job.SeriesID)));
    }

    [Fact]
    public void RemovingASeriesTakesItsSeasonsEpisodesAndTexts()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1, "First")));
        store.SaveSeries(Series("s2", Episode("e2", "s2-1", 1, "Other")));

        Assert.Equal(4, store.RemoveSeries(ID(MetadataEntityType.Series, "s1")));

        Assert.Null(store.GetSeries(ID(MetadataEntityType.Series, "s1")));
        Assert.Equal(["s2"], store.GetAllSeries(TestSources.Plugin).Select(series => series.ID.ID));
        Assert.Equal(["e2"], store.GetAllEpisodes(TestSources.Plugin).Select(episode => episode.ID.ID));
        Assert.Equal(["s2-1", "s2-2"], store.GetAllSeasons(TestSources.Plugin).Select(season => season.ID.ID).Order());
        Assert.All(tables.Texts.GetAll(), text => Assert.NotEqual("s1", text.EntityID.ID));
        Assert.Equal(0, store.RemoveSeries(ID(MetadataEntityType.Series, "s1")));
    }

    [Fact]
    public void RemovingASeriesTakesItsOrderingsItsChoiceAndItsHiddenEpisodes()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        var orderings = tables.OrderingService!;
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1), Episode("e2", "s1-1", 2)));
        store.SaveSeries(Series("s2", Episode("e3", "s2-1", 1)));
        var removed = ID(MetadataEntityType.Series, "s1");
        var other = ID(MetadataEntityType.Series, "s2");
        var local = orderings.CreateLocalOrdering(new()
        {
            SeriesID = removed,
            Name = "Mine",
            Groups = [new() { Name = "All", Episodes = [ID(MetadataEntityType.Episode, "e2"), ID(MetadataEntityType.Episode, "e1")] }],
        });
        var kept = orderings.CreateLocalOrdering(new()
        {
            SeriesID = other,
            Name = "Mine",
            Groups = [new() { Name = "All", Episodes = [ID(MetadataEntityType.Episode, "e3")] }],
        });
        Assert.True(orderings.SetPreferredOrdering(removed, local.ID));
        Assert.True(orderings.SetEpisodeHidden(ID(MetadataEntityType.Episode, "e1"), true));
        Assert.True(orderings.SetEpisodeHidden(ID(MetadataEntityType.Episode, "e3"), true));

        store.RemoveSeries(removed);

        Assert.Null(orderings.GetOrdering(local.ID));
        Assert.Null(tables.RowState!.GetPreferredOrdering(removed));
        Assert.False(orderings.IsEpisodeHidden(ID(MetadataEntityType.Episode, "e1")));
        Assert.NotNull(orderings.GetOrdering(kept.ID));
        Assert.True(orderings.IsEpisodeHidden(ID(MetadataEntityType.Episode, "e3")));

        // Stored again, the series and its episodes start over.
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1), Episode("e2", "s1-1", 2)));
        Assert.Null(tables.RowState.GetPreferredOrdering(removed));
        Assert.False(orderings.IsEpisodeHidden(ID(MetadataEntityType.Episode, "e1")));
    }

    [Fact]
    public void ASaveKeepsTheChosenOrderingAndTheHiddenFlagsOfWhatItKeeps()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        var orderings = tables.OrderingService!;
        var seriesID = ID(MetadataEntityType.Series, "s1");
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1), Episode("e2", "s1-1", 2)));
        var local = orderings.CreateLocalOrdering(new()
        {
            SeriesID = seriesID,
            Name = "Mine",
            Groups = [new() { Name = "All", Episodes = [ID(MetadataEntityType.Episode, "e2"), ID(MetadataEntityType.Episode, "e1")] }],
        });
        var cached = tables.Series.GetByProviderID(TestSources.Plugin, "s1")!;

        Assert.True(orderings.SetPreferredOrdering(seriesID, local.ID));
        Assert.True(orderings.SetEpisodeHidden(ID(MetadataEntityType.Episode, "e1"), true));
        Assert.True(orderings.SetEpisodeHidden(ID(MetadataEntityType.Episode, "e2"), true));

        // The state is written on a copy, and the cached row is replaced.
        Assert.Null(cached.PreferredOrderingID);
        Assert.Equal(local.ID, tables.Series.GetByProviderID(TestSources.Plugin, "s1")?.PreferredOrderingID);

        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1, "Renamed")) with { Rating = 9 });

        Assert.Equal(local.ID, orderings.GetPreferredOrdering(store.GetSeries(seriesID)!).ID);
        Assert.True(orderings.IsEpisodeHidden(ID(MetadataEntityType.Episode, "e1")));
        Assert.False(orderings.IsEpisodeHidden(ID(MetadataEntityType.Episode, "e2")));

        // The dropped episode comes back shown.
        store.SaveSeries(Series("s1", Episode("e1", "s1-1", 1, "Renamed"), Episode("e2", "s1-1", 2)) with { Rating = 9 });
        Assert.True(orderings.IsEpisodeHidden(ID(MetadataEntityType.Episode, "e1")));
        Assert.False(orderings.IsEpisodeHidden(ID(MetadataEntityType.Episode, "e2")));
    }

    [Fact]
    public void ASeriesKeepsOneContentRatingPerCountryAndAChangeToThemUpdatesIt()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();
        var data = Series("s1") with
        {
            ContentRatings =
            [
                new() { CountryCode = "US", Rating = "TV-14" },
                new() { CountryCode = "JP", Rating = "G", LanguageCode = "ja" },
                new() { CountryCode = "us", Rating = "TV-MA" },
            ],
        };
        store.SaveSeries(data);

        var series = store.GetSeries(data.ID)!;
        Assert.Equal(
            [("US", "TV-14", "EN-US"), ("JP", "G", "ja")],
            series.ContentRatings.Select(rating => (rating.CountryCode, rating.Value, rating.LanguageCode))
        );
        Assert.All(series.ContentRatings, rating => Assert.Equal(TestSources.Plugin, rating.Source));
        Assert.Equal(0, store.SaveSeries(data));

        using var events = new Events(data.ID);
        Assert.Equal(1, store.SaveSeries(data with { ContentRatings = [new() { CountryCode = "US", Rating = "TV-PG" }] }));
        Assert.Equal([("US", "TV-PG")], store.GetSeries(data.ID)!.ContentRatings.Select(rating => (rating.CountryCode, rating.Value)));
        Assert.Equal([UpdateReason.Updated], events.Series.Select(args => args.Reason));

        store.RemoveSeries(data.ID);
        Assert.Empty(tables.ContentRatings.GetAll());
    }

    [Fact]
    public void ABlankContentRatingIsRefusedAndNothingIsWritten()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.SeriesStore();

        Assert.Throws<ArgumentException>(() => store.SaveSeries(Series("s1") with { ContentRatings = [new() { CountryCode = "US", Rating = " " }] }));
        Assert.Throws<ArgumentException>(() => store.SaveSeries(Series("s1") with { ContentRatings = [new() { CountryCode = "", Rating = "G" }] }));

        Assert.Null(store.GetSeries(ID(MetadataEntityType.Series, "s1")));
        Assert.Empty(tables.ContentRatings.GetAll());
    }

    #endregion

    #region Movies

    [Fact]
    public void AMovieKeepsItsContentRatingsUntilItIsRemoved()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.MovieStore();
        var data = new MetadataMovieData { ID = ID(MetadataEntityType.Movie, "m1"), ContentRatings = [new() { CountryCode = "DE", Rating = "12" }] };

        Assert.Equal(1, store.SaveMovie(data));
        Assert.Equal("12", Assert.Single(store.GetMovie(data.ID)!.ContentRatings).Value);

        store.RemoveMovie(data.ID);
        Assert.Empty(tables.ContentRatings.GetAll());
    }

    [Fact]
    public void AMovieReadsBackAndIsOnlyUpdatedWhenItChanges()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.MovieStore();
        var data = new MetadataMovieData
        {
            ID = ID(MetadataEntityType.Movie, "m1"),
            Titles = [Title("The Movie")],
            ReleaseDate = new DateOnly(2020, 8, 1),
            Restricted = true,
            Rating = 8,
            RatingVotes = 12,
            Resources = [new() { Type = ResourceType.Website, Name = "Home", Url = "https://example.org" }],
        };
        using var events = new Events(data.ID);

        Assert.Equal(1, store.SaveMovie(data));
        Assert.Equal(0, store.SaveMovie(data));
        Assert.Equal(1, store.SaveMovie(data with { Titles = [Title("The Movie"), Title("Movie", TitleType.Synonym)] }));

        var movie = store.GetMovie(data.ID)!;
        Assert.Equal(new DateTime(2020, 8, 1), movie.ReleaseDate);
        Assert.True(movie.Restricted);
        Assert.Equal(["The Movie", "Movie"], movie.Titles.Select(title => title.Value));
        Assert.Equal(["The Movie"], store.GetAllMovies(TestSources.Plugin).Select(item => item.DefaultTitle.Value));
        Assert.Equal("https://example.org", Assert.Single(((Metadata_Movie)movie).Resources).Url);

        Assert.Equal(1, store.RemoveMovie(data.ID));
        Assert.Null(store.GetMovie(data.ID));
        Assert.Empty(tables.Texts.GetAll());
        Assert.Equal([UpdateReason.Added, UpdateReason.Updated, UpdateReason.Removed], events.Movies.Select(args => args.Reason));
    }

    [Fact]
    public void AMovieKeepsItsOriginalLanguage()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.MovieStore();
        var data = new MetadataMovieData { ID = ID(MetadataEntityType.Movie, "m1"), OriginalLanguageCode = "ja" };

        Assert.Equal(1, store.SaveMovie(data));
        Assert.Equal("ja", store.GetMovie(data.ID)!.OriginalLanguageCode);
        Assert.Equal(0, store.SaveMovie(data));
        Assert.Equal(1, store.SaveMovie(data with { OriginalLanguageCode = " " }));
        Assert.Null(store.GetMovie(data.ID)!.OriginalLanguageCode);
        Assert.Throws<ArgumentException>(() => store.SaveMovie(data with { OriginalLanguageCode = new string('x', 33) }));
    }

    #endregion

    #region Collections

    [Fact]
    public void ACollectionKeepsItsMembersInOrder()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.CollectionStore();
        var collection = ID(MetadataEntityType.Collection, "c1");
        var series = ID(MetadataEntityType.Series, "s1");
        var movie = ID(MetadataEntityType.Movie, "m1");

        Assert.Equal(1, store.SaveCollection(new() { ID = collection, Titles = [Title("Franchise")], Members = [movie, series, movie] }));
        Assert.Equal(0, store.SaveCollection(new() { ID = collection, Titles = [Title("Franchise")], Members = [movie, series] }));

        Assert.Equal([movie, series], store.GetMembers(collection));
        Assert.Equal(["Franchise"], store.GetCollection(collection)!.Titles.Select(title => title.Value));
        Assert.Equal([collection], store.GetCollectionsWith(series).Select(item => item.ID));

        Assert.Equal(1, store.SaveCollection(new() { ID = collection, Members = [series] }));
        Assert.Equal([series], store.GetMembers(collection));
        Assert.Empty(store.GetCollectionsWith(movie));

        Assert.Equal(1, store.RemoveCollection(collection));
        Assert.Null(store.GetCollection(collection));
        Assert.Empty(tables.Members.GetAll());
    }

    [Fact]
    public void ACollectionMemberMustBeASeriesOrMovie()
    {
        var tables = new Tables();
        var store = tables.CollectionStore();
        var collection = ID(MetadataEntityType.Collection, "c1");

        Assert.Throws<ArgumentException>(() => store.SaveCollection(new() { ID = collection, Members = [ID(MetadataEntityType.Episode, "e1")] }));
        Assert.Empty(tables.Collections.GetAll());
    }

    #endregion
}
