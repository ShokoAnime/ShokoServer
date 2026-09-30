using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Airing;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="MetadataService"/> resolves an entry by its
/// <see cref="MetadataGuid"/>: from the core's own tables for the core's
/// sources, and for every other one from a plugin's resolver first and the
/// stores after it, without asking any provider, so a source whose plugin is
/// gone still reads and a source that only links reads nothing.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataServiceLookupTests
{
    #region Helpers

    private static MetadataGuid ID(MetadataSource source, MetadataEntityType entityType, string id)
        => new(source, entityType, id);

    private static Mock<IMetadataResolver> Resolver(string name, MetadataSource source, MetadataEntityType entityType, IMetadata? entry = null)
        => Resolver(name, MetadataEntityScope.Single(source, entityType), entry);

    private static Mock<IMetadataResolver> Resolver(string name, MetadataEntityScope scope, IMetadata? entry = null)
    {
        var resolver = new Mock<IMetadataResolver>();
        resolver.SetupGet(r => r.Name).Returns(name);
        resolver.SetupGet(r => r.Scope).Returns(scope);
        resolver.Setup(r => r.GetEntry(It.IsAny<MetadataGuid>())).Returns(entry);
        return resolver;
    }

    private static IMetadata Entry(MetadataGuid id)
    {
        var entry = new Mock<IMetadata>();
        entry.SetupGet(e => e.ID).Returns(id);
        return entry.Object;
    }

    private static int ErrorsLogged(MetadataLookupTables tables)
        => tables.Logger.Invocations.Count(invocation => invocation.Method.Name == nameof(ILogger.Log) && (LogLevel)invocation.Arguments[0] == LogLevel.Error);

    #endregion

    #region Every Kind

    /// <summary>
    /// One entry of every kind each source holds, and the type it resolves to.
    /// </summary>
    public static TheoryData<string, Type> EveryKind => new()
    {
        { "shoko://series/3", typeof(AnimeSeries) },
        { "shoko://season/3:Episode:1", typeof(AnimeSeason) },
        { "shoko://episode/4", typeof(AnimeEpisode) },
        { "shoko://collection/2", typeof(AnimeGroup) },
        { $"shoko://video/{MetadataLookupTables.VideoHash}+{MetadataLookupTables.VideoSize}", typeof(VideoLocal) },
        { "shoko://video/5", typeof(VideoLocal) },
        { "shoko://user/6", typeof(JMMUser) },
        { "shoko://filter/7", typeof(FilterPreset) },
        { $"shoko://channel/{MetadataLookupTables.ChannelID}", typeof(AiringChannel) },
        { "shoko://ordering/3", typeof(IOrdering) },
        { "user://tag/8", typeof(CustomTag) },
        { "anidb://series/30", typeof(AniDB_Anime) },
        { "anidb://season/30:Special:1", typeof(Shoko.Server.Models.AniDB.Embedded.AniDB_Season) },
        { "anidb://episode/300", typeof(AniDB_Episode) },
        { "anidb://creator/40", typeof(AniDB_Creator) },
        { "anidb://character/41", typeof(AniDB_Character) },
        { "anidb://studio/40", typeof(AniDB_Studio) },
        { "anidb://tag/42", typeof(AniDB_Tag) },
        { "anidb://ordering/30", typeof(IOrdering) },
        { "tmdb://series/5", typeof(TMDB_Show) },
        { "tmdb://season/50", typeof(TMDB_Season) },
        { $"tmdb://season/{MetadataLookupTables.TmdbAlternateSeasonID}", typeof(Shoko.Server.Models.TMDB.TMDB_AlternateOrdering_Season) },
        { "tmdb://episode/55", typeof(TMDB_Episode) },
        { "tmdb://movie/600", typeof(TMDB_Movie) },
        { "tmdb://collection/700", typeof(TMDB_Collection) },
        { "tmdb://creator/80", typeof(TMDB_Person) },
        { "tmdb://studio/81", typeof(TMDB_Company) },
        { "tmdb://network/82", typeof(TMDB_Network) },
        { "tmdb://tag/genre/Drama", typeof(TMDB_Tag) },
        { "tmdb://tag/genre/Animation", typeof(TMDB_Tag) },
        { "tmdb://tag/keyword/isekai", typeof(TMDB_Tag) },
        { "tmdb://ordering/5", typeof(IOrdering) },
        { "test-plugin://series/s1", typeof(Metadata_Series) },
        { "test-plugin://season/s1-1", typeof(Metadata_Season) },
        { "test-plugin://season/g1", typeof(ISeason) },
        { "test-plugin://episode/e1", typeof(Metadata_Episode) },
        { "test-plugin://movie/m1", typeof(Metadata_Movie) },
        { "test-plugin://collection/1", typeof(Metadata_Collection) },
        { "test-plugin://creator/1", typeof(Metadata_Creator) },
        { "test-plugin://character/1", typeof(Metadata_Character) },
        { "test-plugin://tag/1", typeof(Metadata_Tag) },
        { "test-plugin://studio/1", typeof(Metadata_Studio) },
        { "test-plugin://network/1", typeof(Metadata_Network) },
        { "test-plugin://ordering/o1", typeof(IOrdering) },
        { "test-plugin://ordering/default/s1", typeof(IOrdering) },
    };

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void AnEntryOfEveryKindIsFoundByItsID(string idText, Type expectedType)
    {
        var tables = new MetadataLookupTables();
        using var scope = tables.Scope();
        tables.StorePluginEntries();
        var id = MetadataGuid.Parse(idText);

        var entry = tables.Service.GetEntry(id);

        Assert.NotNull(entry);
        Assert.IsAssignableFrom(expectedType, entry);
        // A video's legacy numeric ID resolves too, but the entry keeps its own ID.
        if (idText != "shoko://video/5")
            Assert.Equal(id, entry.ID);
    }

    /// <summary>
    /// IDs that name nothing: a missing row of each core source, a kind a
    /// source holds none of, and IDs that are not valid for their source.
    /// </summary>
    public static TheoryData<string> Nothing => new()
    {
        "shoko://series/99",
        "shoko://series/0",
        "shoko://series/not-a-number",
        "shoko://season/99:Episode:1",
        "shoko://season/3:Nonsense:1",
        "shoko://video/0123456789ABCDEF0123456789ABCDEF+99",
        "shoko://video/+1234",
        $"shoko://video/{MetadataLookupTables.VideoHash}+0",
        "shoko://channel/not-a-guid",
        "shoko://movie/1",
        "shoko://tag/8",
        "user://tag/99",
        "user://series/1",
        "user://season/missing",
        "generated://series/1",
        "anidb://creator/99",
        "anidb://studio/99",
        "anidb://tag/99",
        "anidb://movie/1",
        "anidb://network/1",
        "tmdb://creator/99",
        "tmdb://character/1",
        "tmdb://season/5f0c1a2b3c4d5e6f7a8b9c0e",
        "tmdb://tag/genre/Missing",
        "tmdb://tag/keyword/Drama",
        "tmdb://tag/Drama",
        "tmdb://tag/genre/",
        "anilist://series/1",
        "test-plugin://creator/99",
        "test-plugin://library/1",
    };

    [Theory]
    [MemberData(nameof(Nothing))]
    public void AnIDThatNamesNothingFindsNothing(string idText)
    {
        var tables = new MetadataLookupTables();
        using var scope = tables.Scope();
        tables.StorePluginEntries();

        Assert.Null(tables.Service.GetEntry(MetadataGuid.Parse(idText)));
    }

    [Fact]
    public void AKeywordTooLongForItsIDIsFoundByTheHashOfItsName()
    {
        var tables = new MetadataLookupTables();
        var id = ID(MetadataSource.TMDB, MetadataEntityType.Tag, TMDB_Tag.IDFor(MetadataLookupTables.LongKeyword, TagKind.Keyword));

        var tag = Assert.IsAssignableFrom<ITag>(tables.Service.GetEntry(id));

        Assert.Equal(MetadataLookupTables.LongKeyword, tag.Name);
        Assert.Equal(id, tag.ID);
        Assert.Null(tables.Service.GetEntry(ID(MetadataSource.TMDB, MetadataEntityType.Tag, "keyword/#" + new string('0', 64))));
    }

    #endregion

    #region Typed Lookups

    [Fact]
    public void ATypedLookupOfAnotherTypeFindsNothing()
    {
        var tables = new MetadataLookupTables();
        var creator = ID(MetadataSource.AniDB, MetadataEntityType.Creator, "40");

        Assert.NotNull(tables.Service.GetEntry<ICreator>(creator));
        Assert.NotNull(tables.Service.GetEntry<IMetadata>(creator));
        Assert.Null(tables.Service.GetEntry<ICharacter>(creator));
        Assert.Null(tables.Service.GetEntry<ISeries>(creator));
        // A type that is not one kind is still checked against the entry found.
        Assert.NotNull(tables.Service.GetEntry<Shoko.Abstractions.Metadata.Containers.IWithImages>(creator));
        Assert.Null(tables.Service.GetEntry<ICollection>(creator));
    }

    [Fact]
    public void ATypedLookupOfAnIDOfAnotherCoreKindDoesNotLook()
    {
        var tables = new MetadataLookupTables();
        var id = ID(TestSources.Plugin, MetadataEntityType.Series, "a");
        var resolver = Resolver("Series", TestSources.Plugin, MetadataEntityType.Series, Entry(id));
        tables.Service.AddParts([], [resolver.Object]);

        Assert.Null(tables.Service.GetEntry<ICollection>(id));
        resolver.Verify(r => r.GetEntry(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.NotNull(tables.Service.GetEntry<IMetadata>(id));
    }

    [Fact]
    public void ATypedLookupOfAPluginKindLooksWhateverCoreKindTheTypeIs()
    {
        var tables = new MetadataLookupTables();
        var id = ID(TestSources.Plugin, TestEntityTypes.Library, "a");
        var library = new Mock<ITestLibrary>();
        library.SetupGet(e => e.ID).Returns(id);
        var resolver = Resolver("Library", TestSources.Plugin, TestEntityTypes.Library, library.Object);
        tables.Service.AddParts([], [resolver.Object]);

        Assert.Same(library.Object, tables.Service.GetEntry<ICollection>(id));
        Assert.Same(library.Object, tables.Service.GetEntry<ITestLibrary>(id));
        Assert.Null(tables.Service.GetEntry<ISeries>(id));
        // The typed helpers keep to their own kind.
        Assert.Null(tables.Service.GetCollection(id));
    }

    /// <summary>
    /// A plugin's own kind of entry that is also a collection.
    /// </summary>
    public interface ITestLibrary : ICollection;

    [Fact]
    public void AnIDOfAnotherKindFindsNothing()
    {
        var tables = new MetadataLookupTables();
        using var scope = tables.Scope();
        tables.SeriesStore.SaveSeries(new() { ID = ID(TestSources.Plugin, MetadataEntityType.Series, "s1") });

        Assert.Null(tables.Service.GetEpisode(ID(TestSources.Plugin, MetadataEntityType.Series, "s1")));
        Assert.Null(tables.Service.GetSeries(ID(TestSources.Plugin, MetadataEntityType.Episode, "s1")));
        Assert.Null(tables.Service.GetCollection(ID(MetadataSource.Shoko, MetadataEntityType.Series, "3")));
        Assert.NotNull(tables.Service.GetCollection(ID(MetadataSource.Shoko, MetadataEntityType.Collection, "2")));
        Assert.Throws<ArgumentNullException>(() => tables.Service.GetEntry<ISeries>(null!));
        Assert.Throws<ArgumentNullException>(() => tables.Service.GetEntry(null!));
    }

    #endregion

    #region Resolvers

    [Fact]
    public void AResolverAnswersForAKindTheCoreDoesNotKnow()
    {
        var tables = new MetadataLookupTables();
        var id = ID(TestSources.Plugin, TestEntityTypes.Library, "a");
        var entry = Entry(id);
        var library = Resolver("Library", TestSources.Plugin, TestEntityTypes.Library, entry);
        var otherSource = Resolver("Other", TestSources.AniList, TestEntityTypes.Library, Entry(ID(TestSources.AniList, TestEntityTypes.Library, "a")));
        tables.Service.AddParts([], [library.Object, otherSource.Object]);

        Assert.Same(entry, tables.Service.GetEntry(id));
        library.Verify(r => r.GetEntry(id), Times.Once);
        otherSource.Verify(r => r.GetEntry(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.Equal([library.Object, otherSource.Object], tables.Service.MetadataResolvers);
    }

    [Fact]
    public void AResolverAnswersBeforeTheStores_WhichAnswerWhatItDoesNotHold()
    {
        var tables = new MetadataLookupTables();
        tables.StorePeopleTagsStudiosAndCollections(TestSources.Plugin);
        var creator = ID(TestSources.Plugin, MetadataEntityType.Creator, "1");
        var resolved = Entry(creator);
        var resolver = Resolver("Creators", TestSources.Plugin, MetadataEntityType.Creator, resolved);
        tables.Service.AddParts([], [resolver.Object]);

        Assert.Same(resolved, tables.Service.GetEntry(creator));
        resolver.Verify(r => r.GetEntry(creator), Times.Once);

        resolver.Setup(r => r.GetEntry(It.IsAny<MetadataGuid>())).Returns((IMetadata?)null);
        Assert.IsType<Metadata_Creator>(tables.Service.GetEntry(creator));
        Assert.Null(tables.Service.GetEntry(ID(TestSources.Plugin, MetadataEntityType.Creator, "2")));

        // A kind the resolver did not take is left to the store alone.
        Assert.IsType<Metadata_Tag>(tables.Service.GetEntry(ID(TestSources.Plugin, MetadataEntityType.Tag, "1")));
        resolver.Verify(r => r.GetEntry(It.Is<MetadataGuid>(id => id.EntityType != MetadataEntityType.Creator)), Times.Never);
    }

    /// <summary>
    /// The sources the core registers and answers for itself.
    /// </summary>
    public static TheoryData<string> CoreSources() => new(MetadataSource.All.Where(source => source.IsCore).Select(source => source.Value));

    [Theory]
    [MemberData(nameof(CoreSources))]
    public void AResolverForACoreSourceIsRefusedWhateverItsKind(string sourceValue)
    {
        var tables = new MetadataLookupTables();
        var source = MetadataSource.Get(sourceValue);
        var library = Resolver("CoreLibrary", source, TestEntityTypes.Library, Entry(ID(source, TestEntityTypes.Library, "1")));

        tables.Service.AddParts([], [Resolver("Core", source, MetadataEntityType.Series).Object, library.Object]);

        Assert.Empty(tables.Service.MetadataResolvers);
        Assert.Null(tables.Service.GetEntry(ID(source, TestEntityTypes.Library, "1")));
        library.Verify(r => r.GetEntry(It.IsAny<MetadataGuid>()), Times.Never);
    }

    [Fact]
    public void AResolverKeepsItsPluginPairsWhenItsCorePairsAreRefused()
    {
        var tables = new MetadataLookupTables();
        var id = ID(TestSources.Plugin, TestEntityTypes.Library, "a");
        var entry = Entry(id);
        var scope = MetadataEntityScope.FromPairs([(MetadataSource.TMDB, MetadataEntityType.Series), (TestSources.Plugin, TestEntityTypes.Library)]);
        var resolver = Resolver("Mixed", scope, entry);

        tables.Service.AddParts([], [resolver.Object]);

        Assert.Equal([resolver.Object], tables.Service.MetadataResolvers);
        Assert.Equal(1, ErrorsLogged(tables));
        Assert.Same(entry, tables.Service.GetEntry(id));
        Assert.IsType<Shoko.Server.Models.TMDB.TMDB_Show>(tables.Service.GetEntry(ID(MetadataSource.TMDB, MetadataEntityType.Series, "5")));
        resolver.Verify(r => r.GetEntry(It.Is<MetadataGuid>(guid => guid.Source == MetadataSource.TMDB)), Times.Never);
    }

    [Fact]
    public void ConflictsAreSettledPerPairByLoadOrder()
    {
        var tables = new MetadataLookupTables();
        var library = ID(TestSources.Plugin, TestEntityTypes.Library, "a");
        var series = ID(TestSources.Plugin, MetadataEntityType.Series, "a");
        var anilistLibrary = ID(TestSources.AniList, TestEntityTypes.Library, "a");
        var first = Resolver("First", MetadataEntityScope.ForSource(TestSources.Plugin, TestEntityTypes.Library, MetadataEntityType.Series), Entry(library));
        var second = Resolver("Second", MetadataEntityScope.ForSources([TestSources.Plugin, TestSources.AniList], [TestEntityTypes.Library]), Entry(anilistLibrary));

        tables.Service.AddParts([], [first.Object, second.Object]);

        // The first in load order wins the pair both claim; the second keeps
        // the one nobody else claimed.
        Assert.Equal([first.Object, second.Object], tables.Service.MetadataResolvers);
        Assert.Equal(1, ErrorsLogged(tables));
        Assert.NotNull(tables.Service.GetEntry(library));
        Assert.NotNull(tables.Service.GetEntry(series));
        Assert.NotNull(tables.Service.GetEntry(anilistLibrary));
        first.Verify(r => r.GetEntry(library), Times.Once);
        first.Verify(r => r.GetEntry(series), Times.Once);
        second.Verify(r => r.GetEntry(anilistLibrary), Times.Once);
        second.Verify(r => r.GetEntry(library), Times.Never);
    }

    [Fact]
    public void LaterCallsToAddPartsAreIgnored()
    {
        var tables = new MetadataLookupTables();
        var first = Resolver("First", TestSources.Plugin, TestEntityTypes.Library);

        tables.Service.AddParts([], [first.Object]);
        tables.Service.AddParts([], [Resolver("Late", TestSources.Plugin, MetadataEntityType.Series).Object]);

        Assert.Equal([first.Object], tables.Service.MetadataResolvers);
    }

    [Fact]
    public void AResolverThatThrowsIsLoggedAndFindsNothing()
    {
        var tables = new MetadataLookupTables();
        var resolver = Resolver("Broken", TestSources.Plugin, TestEntityTypes.Library);
        resolver.Setup(r => r.GetEntry(It.IsAny<MetadataGuid>())).Throws(new InvalidOperationException("Broken."));
        tables.Service.AddParts([], [resolver.Object]);

        Assert.Null(tables.Service.GetEntry(ID(TestSources.Plugin, TestEntityTypes.Library, "a")));
        Assert.Equal(1, ErrorsLogged(tables));
    }

    #endregion

    #region Stores and Tables

    [Fact]
    public void EverySourceListsWhatItHolds()
    {
        var tables = new MetadataLookupTables();
        using var scope = tables.Scope();
        tables.StorePluginEntries();
        var service = tables.Service;

        Assert.Equal(["s1"], service.GetAllSeriesForSource(TestSources.Plugin).Select(series => series.ID.ID));
        Assert.Equal(["e1"], service.GetAllEpisodesForSource(TestSources.Plugin).Select(episode => episode.ID.ID));
        Assert.Equal(["m1"], service.GetAllMoviesForSource(TestSources.Plugin).Select(movie => movie.ID.ID));
        Assert.Equal(["5"], service.GetAllSeriesForSource(MetadataSource.TMDB).Select(series => series.ID.ID));

        // A source that only links holds nothing.
        Assert.Empty(service.GetAllSeriesForSource(TestSources.AniList));
    }

    [Fact]
    public void OrderingsAndTheirGroupsAreFoundAsEntries()
    {
        var tables = new MetadataLookupTables();
        using var scope = tables.Scope();
        tables.StorePluginEntries();
        var seriesID = ID(TestSources.Plugin, MetadataEntityType.Series, "s1");
        var local = tables.OrderingService.CreateLocalOrdering(new()
        {
            SeriesID = seriesID,
            Name = "Mine",
            Groups = [new() { Name = "All", Episodes = [ID(TestSources.Plugin, MetadataEntityType.Episode, "e1")] }],
        });
        var globalID = ID(TestSources.Plugin, MetadataEntityType.Ordering, "o1");
        var service = tables.Service;

        Assert.Equal(local.ID, Assert.IsAssignableFrom<IOrdering>(service.GetEntry(local.ID)).ID);
        Assert.Equal(globalID, Assert.IsAssignableFrom<IOrdering>(service.GetEntry(globalID)).ID);
        Assert.True(Assert.IsAssignableFrom<IOrdering>(service.GetEntry(ID(TestSources.Plugin, MetadataEntityType.Ordering, "default/s1"))).IsDefault);
        Assert.Equal(local.ID, service.GetSeason(local.Seasons[0].ID)?.OrderingID);
        Assert.Equal(globalID, service.GetSeason(ID(TestSources.Plugin, MetadataEntityType.Season, "g1"))?.OrderingID);
        Assert.IsType<Metadata_Season>(service.GetSeason(ID(TestSources.Plugin, MetadataEntityType.Season, "s1-1")));
        Assert.Null(service.GetSeason(ID(MetadataSource.User, MetadataEntityType.Season, "missing")));
    }

    [Fact]
    public void AShokoSeasonsLinksAreLookedUpForOneSourceOrEvery()
    {
        var tables = new MetadataLookupTables();
        var series = new Mock<IShokoSeries>();
        series.SetupGet(item => item.AnidbAnimeID).Returns(30);
        var episode = new Mock<IShokoEpisode>();
        episode.SetupGet(item => item.AnidbEpisodeID).Returns(300);
        var season = new Mock<IShokoSeason>();
        season.SetupGet(item => item.Series).Returns(series.Object);
        season.SetupGet(item => item.Episodes).Returns([episode.Object]);
        var seasonID = ID(TestSources.Plugin, MetadataEntityType.Season, "s1-1");
        var link = new Mock<IMetadataEpisodeCrossReference>();
        link.SetupGet(item => item.Source).Returns(TestSources.Plugin);
        link.SetupGet(item => item.AnidbAnimeID).Returns(30);
        link.SetupGet(item => item.AnidbEpisodeID).Returns(300);
        link.SetupGet(item => item.SeasonID).Returns(seasonID);
        link.SetupGet(item => item.SeasonNumber).Returns(1);
        link.SetupGet(item => item.ProviderParentID).Returns(ID(TestSources.Plugin, MetadataEntityType.Series, "s1"));
        var film = new Mock<IMetadataMovieCrossReference>();
        tables.CrossReferences.Setup(store => store.GetEpisodeLinks(300, It.IsAny<MetadataSource?>())).Returns([]);
        tables.CrossReferences.Setup(store => store.GetEpisodeLinks(300, TestSources.Plugin)).Returns([link.Object]);
        tables.CrossReferences.Setup(store => store.GetEpisodeLinks(300, null)).Returns([link.Object]);
        tables.CrossReferences.Setup(store => store.GetMovieLinks(300, It.IsAny<MetadataSource?>())).Returns([]);
        tables.CrossReferences.Setup(store => store.GetMovieLinks(300, TestSources.Plugin)).Returns([film.Object]);
        var service = tables.Service;

        Assert.Same(link.Object, Assert.Single(service.GetEpisodeCrossReferences(season.Object)));
        Assert.Same(link.Object, Assert.Single(service.GetEpisodeCrossReferences(season.Object, TestSources.Plugin)));
        Assert.Empty(service.GetEpisodeCrossReferences(season.Object, TestSources.AniList));
        var seasonLink = Assert.Single(service.GetSeasonCrossReferences(season.Object, TestSources.Plugin));
        Assert.Equal((seasonID, 1, 30), (seasonLink.ProviderID, seasonLink.SeasonNumber, seasonLink.AnidbAnimeID));
        Assert.Empty(service.GetSeasonCrossReferences(season.Object, TestSources.AniList));
        Assert.Same(film.Object, Assert.Single(service.GetMovieCrossReferences(season.Object, TestSources.Plugin)));
        Assert.Empty(service.GetMovieCrossReferences(season.Object));
    }

    [Fact]
    public void ThePluginSourcesLinksAreReadFromTheSharedTables()
    {
        var tables = new MetadataLookupTables();
        var entry = ID(TestSources.AniList, MetadataEntityType.Series, "7");
        var link = new CrossRef_AniDB_Metadata_Series { Source = TestSources.AniList, AnidbAnimeID = 30, ProviderID = "7" };
        tables.CrossReferences.Setup(store => store.GetLinksTo(entry)).Returns([link]);

        Assert.Same(link, Assert.Single(tables.Service.GetCrossReferencesForProviderEntry(entry)));
        Assert.Empty(tables.Service.GetCrossReferencesForProviderEntry(ID(MetadataSource.User, MetadataEntityType.Series, "7")));
    }

    [Fact]
    public void TheCollectionsAnEntryIsIn_AreReadFromWhereTheEntryIsKept()
    {
        var tables = new MetadataLookupTables();
        var service = tables.Service;
        var movie = ID(TestSources.Plugin, MetadataEntityType.Movie, "9");
        tables.StorePeopleTagsStudiosAndCollections(TestSources.Plugin);
        tables.CollectionMembers.Cache.Update(new Metadata_Collection_Member
        {
            Metadata_Collection_MemberID = 1,
            Source = TestSources.Plugin,
            CollectionID = "1",
            MemberType = MetadataEntityType.Movie,
            MemberID = "9",
        });

        Assert.Equal(ID(TestSources.Plugin, MetadataEntityType.Collection, "1"), Assert.Single(service.GetCollectionsWith(movie)).ID);
        Assert.Equal(2, Assert.IsType<AnimeGroup>(Assert.Single(service.GetCollectionsWith(ID(MetadataSource.Shoko, MetadataEntityType.Series, "3")))).AnimeGroupID);
        Assert.Equal(1, Assert.IsType<AnimeGroup>(Assert.Single(service.GetCollectionsWith(ID(MetadataSource.Shoko, MetadataEntityType.Collection, "2")))).AnimeGroupID);
        Assert.Empty(service.GetCollectionsWith(ID(MetadataSource.Shoko, MetadataEntityType.Collection, "1")));
        Assert.Empty(service.GetCollectionsWith(ID(MetadataSource.Shoko, MetadataEntityType.Series, "0")));
        Assert.Empty(service.GetCollectionsWith(ID(MetadataSource.AniDB, MetadataEntityType.Series, "30")));
        Assert.Empty(service.GetCollectionsWith(ID(MetadataSource.TMDB, MetadataEntityType.Series, "5")));
        Assert.Empty(service.GetCollectionsWith(ID(MetadataSource.TMDB, MetadataEntityType.Movie, "404")));
        Assert.Equal(700, Assert.IsType<TMDB_Collection>(Assert.Single(service.GetCollectionsWith(ID(MetadataSource.TMDB, MetadataEntityType.Movie, "600")))).TmdbCollectionID);
        Assert.Empty(service.GetCollectionsWith(ID(MetadataSource.TMDB, MetadataEntityType.Movie, "601")));
        Assert.Throws<ArgumentNullException>(() => service.GetCollectionsWith(null!));
    }

    #endregion
}
