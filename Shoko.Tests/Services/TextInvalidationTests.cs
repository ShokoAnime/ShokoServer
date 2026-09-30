using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers that every reader of a Shoko entry's text sees a write at once: the series, episode and
/// group models, the filters' name sets and the series search each read the new text straight after
/// the text, a row or a link changed, although each keeps what it worked out.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class TextInvalidationTests
{
    private const int AnimeID = 1;

    private const int AnidbEpisodeID = 10;

    private const int SecondAnimeID = 7;

    private static readonly MetadataGuid AnimeEntry = new(MetadataSource.AniDB, MetadataEntityType.Series, AnimeID.ToString());

    private static readonly MetadataGuid SecondAnimeEntry = new(MetadataSource.AniDB, MetadataEntityType.Series, SecondAnimeID.ToString());

    private static readonly MetadataGuid AnidbEpisodeEntry = new(MetadataSource.AniDB, MetadataEntityType.Episode, AnidbEpisodeID.ToString());

    private static readonly MetadataGuid ShowEntry = new(MetadataSource.TMDB, MetadataEntityType.Series, "5");

    private static readonly MetadataGuid TmdbEpisodeEntry = new(MetadataSource.TMDB, MetadataEntityType.Episode, "50");

    #region World

    /// <summary>
    /// One anime with one episode, its Shoko series, episode and group, and a TMDB show and episode
    /// whose links the test adds, all reading their texts from one cache-only store. The group can
    /// hold a second series, of a second anime, after the first.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public MetadataTextStore Store { get; } = new(new TextCache(), new CacheOnlyRowWriter());

        public MetadataTextManager Manager { get; }

        public List<IMetadataSeriesCrossReference> SeriesLinks { get; } = [];

        public List<IMetadataEpisodeCrossReference> EpisodeLinks { get; } = [];

        public List<IMetadataSeasonCrossReference> SeasonLinks { get; } = [];

        public AniDB_Anime Anime { get; } = new() { AniDB_AnimeID = AnimeID, AnimeID = AnimeID, MainTitle = "Old Name" };

        public AniDB_Episode AnidbEpisode { get; } = new() { AniDB_EpisodeID = AnidbEpisodeID, EpisodeID = AnidbEpisodeID, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode, EpisodeNumber = 1 };

        public AnimeSeries Series { get; } = new() { AnimeSeriesID = 2, AniDB_ID = AnimeID, AnimeGroupID = 3 };

        public AnimeEpisode Episode { get; } = new() { AnimeEpisodeID = 4, AnimeSeriesID = 2, AniDB_EpisodeID = AnidbEpisodeID };

        public AnimeGroup Group { get; } = new() { AnimeGroupID = 3 };

        public AniDB_Anime SecondAnime { get; } = new() { AniDB_AnimeID = SecondAnimeID, AnimeID = SecondAnimeID, MainTitle = "Second Name" };

        public AnimeSeries SecondSeries { get; } = new() { AnimeSeriesID = 6, AniDB_ID = SecondAnimeID, AnimeGroupID = 3 };

        public AnimeSeriesRepository SeriesRepository { get; }

        public AniDB_EpisodeRepository AnidbEpisodeRepository { get; }

        public CrossRef_AniDB_Metadata_SeriesRepository SeriesLinkRepository { get; }

        public CrossRef_AniDB_Metadata_EpisodeRepository EpisodeLinkRepository { get; }

        public World(bool withSecondSeries = false)
        {
            var service = new Mock<IMetadataService>();
            service.Setup(s => s.GetSeriesCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>()))
                .Returns((int animeID, MetadataSource? source) => [.. SeriesLinks.Where(link => link.AnidbAnimeID == animeID && (source is null || link.Source == source))]);
            service.Setup(s => s.GetEpisodeCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>()))
                .Returns((int episodeID, MetadataSource? source) => [.. EpisodeLinks.Where(link => link.AnidbEpisodeID == episodeID && (source is null || link.Source == source))]);
            service.Setup(s => s.GetMovieCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetMovieCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetSeasonCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>()))
                .Returns((int animeID, MetadataSource? source) => [.. SeasonLinks.Where(link => link.AnidbAnimeID == animeID && (source is null || link.Source == source))]);
            service.Setup(s => s.GetEpisodeCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetCollectionsWith(It.IsAny<MetadataGuid>())).Returns([]);
            Manager = TestTextManager.Build(Store, service.Object);

            SeriesRepository = CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID, withSecondSeries ? [Series, SecondSeries] : [Series]);
            AnidbEpisodeRepository = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, AnidbEpisode);
            SeriesLinkRepository = CachedRepo.Build<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(xref => xref.CrossRef_AniDB_Metadata_SeriesID);
            EpisodeLinkRepository = CachedRepo.Build<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(xref => xref.CrossRef_AniDB_Metadata_EpisodeID);
            _scope = new RepoFactoryScope()
                .Set(Manager)
                .Set(SeriesRepository)
                .Set(AnidbEpisodeRepository)
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID, withSecondSeries ? [Anime, SecondAnime] : [Anime])
                .With<AnimeEpisodeRepository, int, AnimeEpisode>(episode => episode.AnimeEpisodeID, [Episode])
                .With<AnimeGroupRepository, int, AnimeGroup>(group => group.AnimeGroupID, [Group]);

            SetAnimeTitle("Old Name");
            if (withSecondSeries)
                Store.SetTitles(SecondAnimeEntry, MetadataSource.AniDB, [Title(MetadataSource.AniDB, "Second Name", TitleLanguage.Romaji, "x-jat", TitleType.Main)]);
            Store.SetTitles(AnidbEpisodeEntry, MetadataSource.AniDB, [Title(MetadataSource.AniDB, "Old Episode", TitleLanguage.English, "en", TitleType.None)]);
            SeriesSearch.MarkDirty();
        }

        public void SetAnimeTitle(string value)
            => Store.SetTitles(AnimeEntry, MetadataSource.AniDB, [Title(MetadataSource.AniDB, value, TitleLanguage.Romaji, "x-jat", TitleType.Main)]);

        /// <summary>
        /// Links the anime to the TMDB show and its episode to the TMDB episode, as the linking
        /// service does, whose rows reach the cache through the link repository.
        /// </summary>
        public void Link()
        {
            SeriesLinks.Add(SeriesLink());
            EpisodeLinks.Add(EpisodeLink());
            var season = new Mock<IMetadataSeasonCrossReference>();
            season.SetupGet(l => l.AnidbAnimeID).Returns(AnimeID);
            season.SetupGet(l => l.Source).Returns(MetadataSource.TMDB);
            season.SetupGet(l => l.SeasonNumber).Returns(1);
            season.SetupGet(l => l.ProviderID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Season, "5"));
            SeasonLinks.Add(season.Object);
            AfterSave(SeriesLinkRepository, new CrossRef_AniDB_Metadata_Series { CrossRef_AniDB_Metadata_SeriesID = 1, AnidbAnimeID = AnimeID, Source = MetadataSource.TMDB, ProviderID = ShowEntry.ID });
            AfterSave(EpisodeLinkRepository, new CrossRef_AniDB_Metadata_Episode
            {
                CrossRef_AniDB_Metadata_EpisodeID = 1,
                AnidbAnimeID = AnimeID,
                AnidbEpisodeID = AnidbEpisodeID,
                Source = MetadataSource.TMDB,
                ProviderID = TmdbEpisodeEntry.ID,
            });
        }

        private IMetadataSeriesCrossReference SeriesLink()
        {
            var show = new Mock<ISeries>();
            show.SetupGet(s => s.ID).Returns(ShowEntry);
            show.SetupGet(s => s.Source).Returns(MetadataSource.TMDB);
            show.SetupGet(s => s.Titles).Returns(() => Store.GetTitles(ShowEntry, MetadataSource.TMDB));
            show.SetupGet(s => s.Overviews).Returns(() => Store.GetOverviews(ShowEntry, MetadataSource.TMDB));
            var link = new Mock<IMetadataSeriesCrossReference>();
            link.SetupGet(l => l.AnidbAnimeID).Returns(AnimeID);
            link.SetupGet(l => l.Source).Returns(MetadataSource.TMDB);
            link.SetupGet(l => l.ProviderID).Returns(ShowEntry);
            link.SetupGet(l => l.Provider).Returns(show.Object);
            return link.Object;
        }

        private IMetadataEpisodeCrossReference EpisodeLink()
        {
            var episode = new Mock<IEpisode>();
            episode.SetupGet(e => e.ID).Returns(TmdbEpisodeEntry);
            episode.SetupGet(e => e.Source).Returns(MetadataSource.TMDB);
            episode.SetupGet(e => e.Titles).Returns(() => Store.GetTitles(TmdbEpisodeEntry, MetadataSource.TMDB));
            episode.SetupGet(e => e.Overviews).Returns(() => Store.GetOverviews(TmdbEpisodeEntry, MetadataSource.TMDB));
            var link = new Mock<IMetadataEpisodeCrossReference>();
            link.SetupGet(l => l.AnidbAnimeID).Returns(AnimeID);
            link.SetupGet(l => l.AnidbEpisodeID).Returns(AnidbEpisodeID);
            link.SetupGet(l => l.Source).Returns(MetadataSource.TMDB);
            link.SetupGet(l => l.ProviderID).Returns(TmdbEpisodeEntry);
            link.SetupGet(l => l.Provider).Returns(episode.Object);
            return link.Object;
        }

        public void Dispose()
        {
            _scope.Dispose();
            SeriesSearch.MarkDirty();
        }
    }

    private static TitleStub Title(MetadataSource source, string value, TitleLanguage language, string code, TitleType type)
        => new() { Source = source, Value = value, Language = language, LanguageCode = code, Type = type };

    private static TextStub Overview(MetadataSource source, string value)
        => new() { Source = source, Value = value, Language = TitleLanguage.English, LanguageCode = "en" };

    /// <summary>
    /// Puts a saved row in a repository's cache the way a committed save does, which runs the
    /// repository's own cache hooks.
    /// </summary>
    private static void AfterSave<T>(object repository, T row)
    {
        var method = repository.GetType().GetMethod("UpdateCache", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(T)])!;
        method.Invoke(repository, [row]);
    }

    #endregion

    [Fact]
    public void TheSeriesTheGroupAndTheirFiltersReadAnAnimeTitleWrittenAfterTheirFirstRead()
    {
        using var world = new World();
        var filterable = new FilterableAnimeSeries(world.Series, DateTime.Now);
        var names = filterable.Names;
        Assert.Equal("Old Name", world.Series.Title);
        Assert.Equal("Old Name", world.Group.GroupName);
        Assert.Contains("Old Name", names);
        Assert.Same(names, filterable.Names);

        world.SetAnimeTitle("New Name");

        Assert.Equal("New Name", world.Series.Title);
        Assert.Equal("New Name", world.Series.DefaultTitle.Value);
        Assert.Equal("New Name", world.Series.Titles[0].Value);
        Assert.Equal("New Name", world.Group.GroupName);
        Assert.Contains("New Name", filterable.Names);
        Assert.DoesNotContain("Old Name", filterable.Names);
        Assert.Contains("New Name", new FilterableAnimeGroup(world.Group, DateTime.Now).Names);
    }

    [Fact]
    public void TheSeriesSearchFindsAnAnimeByATitleWrittenAfterTheIndexWasBuilt()
    {
        using var world = new World();
        var user = new JMMUser { JMMUserID = 1 };
        Assert.Single(SeriesSearch.SearchSeries(user, "Old Name", 10, SeriesSearch.SearchFlags.Titles));

        world.SetAnimeTitle("Brand New");

        Assert.Empty(SeriesSearch.SearchSeries(user, "Old Name", 10, SeriesSearch.SearchFlags.Titles));
        Assert.Equal(world.Series.AnimeSeriesID, Assert.Single(SeriesSearch.SearchSeries(user, "Brand New", 10, SeriesSearch.SearchFlags.Titles)).Result.AnimeSeriesID);
    }

    [Fact]
    public void AUsersNameReachesTheSeriesTheGroupAndTheFiltersAtOnce()
    {
        using var world = new World();
        var groupFilter = new FilterableAnimeGroup(world.Group, DateTime.Now);
        Assert.Equal("Old Name", world.Series.Title);
        Assert.DoesNotContain("Picked", groupFilter.Names);

        Assert.True(world.Manager.SetCustomTitle(((IMetadata)world.Series).ID, "Picked"));
        Assert.Equal("Picked", world.Series.Title);
        Assert.Equal("Picked", world.Group.GroupName);
        Assert.Contains("Picked", groupFilter.Names);

        Assert.True(world.Manager.SetCustomTitle(((IMetadata)world.Group).ID, "Group Name"));
        Assert.Equal("Group Name", world.Group.GroupName);
        Assert.Contains("Group Name", new FilterableAnimeSeries(world.Series, DateTime.Now).Names);

        Assert.True(world.Manager.SetCustomTitle(((IMetadata)world.Series).ID, null));
        Assert.Equal("Old Name", world.Series.Title);
    }

    [Fact]
    public void AnEpisodeReadsItsAnidbTitleAndAnOverviewSavedOnItsRow()
    {
        using var world = new World();
        Assert.Equal("Old Episode", world.Episode.Title);
        Assert.Null(world.Episode.PreferredOverview);

        world.Store.SetTitles(AnidbEpisodeEntry, MetadataSource.AniDB, [Title(MetadataSource.AniDB, "New Episode", TitleLanguage.English, "en", TitleType.None)]);
        Assert.Equal("New Episode", world.Episode.Title);

        world.AnidbEpisode.Description = "Saved on the row.";
        AfterSave(world.AnidbEpisodeRepository, world.AnidbEpisode);
        Assert.Equal("Saved on the row.", world.Episode.PreferredOverview?.Value);
        Assert.Equal("Saved on the row.", ((IWithOverviews)world.AnidbEpisode).PreferredOverview?.Value);
    }

    [Fact]
    public void AnAnimeOverviewSavedOnItsRowReachesTheSeriesAndTheGroup()
    {
        using var world = new World();
        Assert.Null(world.Series.PreferredOverview);
        Assert.Equal(string.Empty, world.Group.Description);

        world.Anime.Description = "Told by AniDB.";
        AfterSave(RepoFactory.AniDB_Anime, world.Anime);

        Assert.Equal("Told by AniDB.", world.Series.PreferredOverview?.Value);
        Assert.Equal("Told by AniDB.", world.Group.Description);
        Assert.Equal("Told by AniDB.", ((IWithOverviews)world.Anime).PreferredOverview?.Value);
    }

    [Fact]
    public void ALinkAndThenTheLinkedEntriesTextsReachTheSeriesAndTheEpisode()
    {
        using var world = new World();
        Assert.DoesNotContain(world.Series.Titles, title => title.Source == MetadataSource.TMDB);
        Assert.Null(world.Series.PreferredOverview);
        Assert.Equal("Old Episode", world.Episode.Title);

        // The link is saved before the show's texts are.
        world.Link();
        Assert.DoesNotContain(world.Series.Titles, title => title.Source == MetadataSource.TMDB);

        world.Store.SetTitles(ShowEntry, MetadataSource.TMDB, [Title(MetadataSource.TMDB, "The Show", TitleLanguage.English, "en", TitleType.Official)]);
        world.Store.SetOverviews(ShowEntry, MetadataSource.TMDB, [Overview(MetadataSource.TMDB, "Told by TMDB.")]);
        world.Store.SetTitles(TmdbEpisodeEntry, MetadataSource.TMDB, [Title(MetadataSource.TMDB, "Linked Episode", TitleLanguage.English, "en", TitleType.Official)]);

        Assert.Contains(world.Series.Titles, title => title.Value == "The Show");
        Assert.Equal("Told by TMDB.", world.Series.PreferredOverview?.Value);
        Assert.Equal("Linked Episode", world.Episode.Title);
    }

    [Fact]
    public void ASeriesMovedIntoAGroupIsNamedAndFilteredByItsNewGroup()
    {
        using var world = new World();
        var other = new AnimeGroup { AnimeGroupID = 9 };
        AfterSave(RepoFactory.AnimeGroup, other);
        Assert.DoesNotContain("Named Group", new FilterableAnimeSeries(world.Series, DateTime.Now).Names);
        world.Manager.SetCustomTitle(((IMetadata)other).ID, "Named Group");
        Assert.DoesNotContain("Named Group", new FilterableAnimeSeries(world.Series, DateTime.Now).Names);

        world.Series.AnimeGroupID = other.AnimeGroupID;
        AfterSave(world.SeriesRepository, world.Series);

        Assert.Contains("Named Group", new FilterableAnimeSeries(world.Series, DateTime.Now).Names);
        Assert.Throws<InvalidOperationException>(() => world.Group.GroupName);
        world.Manager.SetCustomTitle(((IMetadata)other).ID, null);
        Assert.Equal("Old Name", other.GroupName);
    }

    [Fact]
    public void TheGroupASeriesLeftIsNamedAndFilteredByTheSeriesStillInIt()
    {
        using var world = new World(withSecondSeries: true);
        var groupFilter = new FilterableAnimeGroup(world.Group, DateTime.Now);
        Assert.Equal("Old Name", world.Group.GroupName);
        Assert.Contains("Old Name", groupFilter.Names);
        Assert.Contains("Second Name", groupFilter.Names);

        // Only the series is saved, as a move by the group manager does when
        // the series was not the one the group was told to name itself by.
        var other = new AnimeGroup { AnimeGroupID = 9 };
        AfterSave(RepoFactory.AnimeGroup, other);
        world.Series.AnimeGroupID = other.AnimeGroupID;
        AfterSave(world.SeriesRepository, world.Series);

        Assert.Equal("Second Name", world.Group.GroupName);
        groupFilter = new FilterableAnimeGroup(world.Group, DateTime.Now);
        Assert.DoesNotContain("Old Name", groupFilter.Names);
        Assert.Contains("Second Name", groupFilter.Names);
        Assert.Equal("Old Name", other.GroupName);
    }

    [Fact]
    public void ASeriesSavedInTheSameGroupKeepsTheGroupsTexts()
    {
        using var world = new World();
        var names = new FilterableAnimeGroup(world.Group, DateTime.Now).Names;
        Assert.Equal("Old Name", world.Group.GroupName);

        AfterSave(world.SeriesRepository, world.Series);
        AfterSave(RepoFactory.AnimeGroup, world.Group);

        Assert.Same(names, new FilterableAnimeGroup(world.Group, DateTime.Now).Names);
    }

    [Fact]
    public void ALanguageSettingsChangeWorksEveryChoiceOutAgain()
    {
        using var world = new World();
        var names = new FilterableAnimeSeries(world.Series, DateTime.Now).Names;
        Assert.Equal("Old Name", world.Series.Title);

        MetadataTextManager.OnLanguageSettingsChanged();

        Assert.NotSame(names, new FilterableAnimeSeries(world.Series, DateTime.Now).Names);
        Assert.Equal("Old Name", world.Series.Title);
    }
}
