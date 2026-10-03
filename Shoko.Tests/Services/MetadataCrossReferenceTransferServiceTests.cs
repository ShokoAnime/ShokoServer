using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataCrossReferenceTransferService"/>: every source's
/// CSV file is written and read in one format under the source's own name, an
/// older file's <c>0</c> still reads as a link to nothing, and an import writes
/// nothing when a line cannot be read.
/// </summary>
public class MetadataCrossReferenceTransferServiceTests
{
    #region Fixtures

    private sealed class Harness
    {
        public List<CrossRef_AniDB_Metadata_Series> Series { get; } = [];

        public List<CrossRef_AniDB_Metadata_Movie> Movies { get; } = [];

        public List<CrossRef_AniDB_Metadata_Episode> Episodes { get; } = [];

        public Mock<IMetadataCrossReferenceStore> Store { get; } = new();

        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IMetadataRefreshService> Refresh { get; } = new();

        public List<(IReadOnlyList<MetadataLinkData> Links, IReadOnlyList<IMetadataCrossReference> Removals)> Merged { get; } = [];

        public Harness()
        {
            Store.Setup(s => s.GetAllSeriesLinks(It.IsAny<MetadataSource?>()))
                .Returns((MetadataSource? source) => [.. Series.Where(x => x.Source == source)]);
            Store.Setup(s => s.GetAllMovieLinks(It.IsAny<MetadataSource?>()))
                .Returns((MetadataSource? source) => [.. Movies.Where(x => x.Source == source)]);
            Store.Setup(s => s.GetAllEpisodeLinks(It.IsAny<MetadataSource?>()))
                .Returns((MetadataSource? source) => [.. Episodes.Where(x => x.Source == source)]);
            Store.Setup(s => s.GetEpisodeLinksForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>()))
                .Returns((int animeID, MetadataSource? source) => [.. Episodes.Where(x => x.Source == source && x.AnidbAnimeID == animeID)]);
            Store.Setup(s => s.MergeSeriesLinks(It.IsAny<IEnumerable<MetadataSeriesLinkData>>(), It.IsAny<IEnumerable<IMetadataSeriesCrossReference>?>(),
                    It.IsAny<MetadataLinkUpdateOptions?>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<MetadataSeriesLinkData> links, IEnumerable<IMetadataSeriesCrossReference>? removals, MetadataLinkUpdateOptions? _, CancellationToken _) =>
                    Merged.Add(([.. links], [.. removals ?? []])))
                .ReturnsAsync([]);
            Store.Setup(s => s.MergeMovieLinks(It.IsAny<IEnumerable<MetadataMovieLinkData>>(), It.IsAny<IEnumerable<IMetadataMovieCrossReference>?>(),
                    It.IsAny<MetadataLinkUpdateOptions?>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<MetadataMovieLinkData> links, IEnumerable<IMetadataMovieCrossReference>? removals, MetadataLinkUpdateOptions? _, CancellationToken _) =>
                    Merged.Add(([.. links], [.. removals ?? []])))
                .ReturnsAsync([]);
            Store.Setup(s => s.MergeEpisodeLinks(It.IsAny<IEnumerable<MetadataEpisodeLinkData>>(), It.IsAny<IEnumerable<IMetadataEpisodeCrossReference>?>(),
                    It.IsAny<MetadataLinkUpdateOptions?>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<MetadataEpisodeLinkData> links, IEnumerable<IMetadataEpisodeCrossReference>? removals, MetadataLinkUpdateOptions? _, CancellationToken _) =>
                    Merged.Add(([.. links], [.. removals ?? []])))
                .ReturnsAsync([]);
            Refresh.Setup(r => r.RefreshEntry(It.IsAny<MetadataGuid>(), It.IsAny<bool>(), It.IsAny<MetadataRefreshOptions?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        public MetadataCrossReferenceTransferService Build()
            => new(
                Store.Object,
                Metadata.Object,
                Refresh.Object,
                CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID),
                CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID),
                NullLogger<MetadataCrossReferenceTransferService>.Instance
            );

        public Task<MetadataCrossReferenceImportResult> Import(MetadataSource source, string text, MetadataCrossReferenceImportOptions? options = null)
            => Build().Import(source, new StringReader(text), options, TestContext.Current.CancellationToken);
    }

    private static Harness Tmdb()
    {
        var harness = new Harness();
        var tmdb = MetadataSource.TMDB;
        harness.Series.AddRange([
            new() { Source = tmdb, AnidbAnimeID = 1, ProviderID = "5", MatchRating = MatchRating.UserVerified },
            new() { Source = tmdb, AnidbAnimeID = 2, ProviderID = "6", MatchRating = MatchRating.TitleMatches },
            new() { Source = tmdb, AnidbAnimeID = 3, ProviderID = string.Empty, MatchRating = MatchRating.UserVerified },
            new() { Source = TestSources.Plugin, AnidbAnimeID = 1, ProviderID = "elsewhere" },
        ]);
        harness.Movies.Add(new() { Source = tmdb, AnidbAnimeID = 1, AnidbEpisodeID = 12, ProviderID = "7", MatchRating = MatchRating.DateMatches });
        harness.Episodes.AddRange([
            new() { Source = tmdb, AnidbAnimeID = 1, AnidbEpisodeID = 10, ProviderID = "55", ProviderParentID = "5", MatchRating = MatchRating.DateAndTitleMatches },
            new() { Source = tmdb, AnidbAnimeID = 1, AnidbEpisodeID = 11, ProviderID = string.Empty, ProviderParentID = string.Empty, MatchRating = MatchRating.UserVerified },
        ]);
        return harness;
    }

    private const string TmdbFile = """
        AnidbAnimeId,AnidbEpisodeId,TmdbMovieId,Rating
        1,12,7,DateMatches
        AnidbAnimeId,TmdbShowId,Rating
        1,5,UserVerified
        2,6,TitleMatches
        AnidbAnimeId,AnidbEpisodeId,TmdbShowId,TmdbEpisodeId,Rating
        1,10,5,55,DateAndTitleMatches
        1,11,,,UserVerified

        """;

    #endregion

    #region Export

    [Fact]
    public void TmdbIsWrittenInTheSharedFormat()
    {
        var text = Tmdb().Build().Export(MetadataSource.TMDB);

        Assert.Equal(TmdbFile.ReplaceLineEndings(), text);
    }

    [Fact]
    public void TheFiltersPickTheLinksAndTheSections()
    {
        var harness = Tmdb();
        var service = harness.Build();

        Assert.Equal(
            "AnidbAnimeId,TmdbShowId,Rating\n2,6,TitleMatches\n".ReplaceLineEndings(),
            service.Export(MetadataSource.TMDB, new() { Sections = MetadataCrossReferenceSections.Series, Automatic = true })
        );
        Assert.Equal(
            "AnidbAnimeId,TmdbShowId,Rating\n1,5,UserVerified\n".ReplaceLineEndings(),
            service.Export(MetadataSource.TMDB, new() { Sections = MetadataCrossReferenceSections.Series, WithEpisodes = true })
        );
        Assert.Equal(
            "AnidbAnimeId,AnidbEpisodeId,TmdbShowId,TmdbEpisodeId,Rating\n1,11,,,UserVerified\n".ReplaceLineEndings(),
            service.Export(MetadataSource.TMDB, new() { Sections = MetadataCrossReferenceSections.Episode, ProviderEpisodeID = string.Empty })
        );
        Assert.Empty(service.Export(MetadataSource.TMDB, new() { Sections = MetadataCrossReferenceSections.None }));
        Assert.Empty(service.Export(MetadataSource.TMDB, new() { Sections = MetadataCrossReferenceSections.Movie, AnidbEpisodeID = 99 }));
    }

    [Fact]
    public void FilmsAreListedByAnimeEpisodeAndFilm()
    {
        var harness = new Harness();
        var tmdb = MetadataSource.TMDB;
        harness.Movies.AddRange([
            new() { Source = tmdb, AnidbAnimeID = 2, AnidbEpisodeID = 20, ProviderID = "97787", MatchRating = MatchRating.UserVerified },
            new() { Source = tmdb, AnidbAnimeID = 2, AnidbEpisodeID = 20, ProviderID = "20455", MatchRating = MatchRating.UserVerified },
            new() { Source = tmdb, AnidbAnimeID = 1, AnidbEpisodeID = 13, ProviderID = "8", MatchRating = MatchRating.UserVerified },
            new() { Source = tmdb, AnidbAnimeID = 1, AnidbEpisodeID = 12, ProviderID = "9", MatchRating = MatchRating.UserVerified },
        ]);

        Assert.Equal(
            "AnidbAnimeId,AnidbEpisodeId,TmdbMovieId,Rating\n1,12,9,UserVerified\n1,13,8,UserVerified\n2,20,20455,UserVerified\n2,20,97787,UserVerified\n"
                .ReplaceLineEndings(),
            harness.Build().Export(tmdb, new() { Sections = MetadataCrossReferenceSections.Movie })
        );
    }

    [Fact]
    public void CommentsNameWhatEachLinkJoins()
    {
        var harness = Tmdb();
        var show = new Mock<ISeries>();
        show.SetupGet(s => s.DefaultTitle).Returns(new Abstractions.Metadata.Stub.TitleStub { Source = MetadataSource.TMDB, Language = TitleLanguage.English, LanguageCode = "en", Value = "The Show" });
        harness.Metadata.Setup(m => m.GetEntry(new(MetadataSource.TMDB, MetadataEntityType.Series, "5"))).Returns(show.Object);

        var text = harness.Build().Export(MetadataSource.TMDB, new() { Sections = MetadataCrossReferenceSections.Series, IncludeComments = true, AnidbAnimeID = 1 });

        var lines = text.Split(Environment.NewLine);
        var data = Array.IndexOf(lines, "1,5,UserVerified");
        Assert.StartsWith("#", lines[data - 1]);
        Assert.Contains("(a1)", lines[data - 1]);
        Assert.Contains("``The Show`` (s5)", lines[data - 1]);
    }

    [Fact]
    public void AnotherSourceUsesItsOwnNameAndQuotesWhatNeedsIt()
    {
        var harness = new Harness();
        harness.Series.Add(new() { Source = TestSources.Plugin, AnidbAnimeID = 1, ProviderID = "a,\"b\"", MatchRating = MatchRating.UserVerified });
        harness.Episodes.Add(new() { Source = TestSources.Plugin, AnidbAnimeID = 1, AnidbEpisodeID = 10, ProviderID = string.Empty, ProviderParentID = string.Empty });

        var text = harness.Build().Export(TestSources.Plugin);

        var prefix = string.Concat(TestSources.Plugin.Value.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        Assert.Equal(
            $"AnidbAnimeId,{prefix}ShowId,Rating\n1,\"a,\"\"b\"\"\",UserVerified\nAnidbAnimeId,AnidbEpisodeId,{prefix}ShowId,{prefix}EpisodeId,Rating\n1,10,,,None\n"
                .ReplaceLineEndings(),
            text
        );
        Assert.Equal(["a", "b,c", "d\"e", ""], MetadataCrossReferenceTransferService.SplitLine("a,\"b,c\",\"d\"\"e\","));
    }

    #endregion

    #region Import

    [Fact]
    public async Task AnExportedTmdbFileImportsUnchanged()
    {
        var harness = Tmdb();

        var result = await harness.Import(MetadataSource.TMDB, TmdbFile);

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.LinkCount);
        Assert.Equal(0, result.MoviesAdded + result.MoviesUpdated + result.MoviesRemoved + result.SeriesAdded);
        Assert.Equal(1, result.MoviesKept);
        Assert.Equal(2, result.EpisodesKept);
        Assert.Equal(0, result.EpisodesAdded + result.EpisodesUpdated + result.EpisodesRemoved);
        Assert.Empty(harness.Merged);
    }

    [Fact]
    public async Task AnImportIsOneLinkEvent()
    {
        var tracker = new MetadataLinkChangeTracker();
        var links = new WritableLinkStore(tracker);
        var raised = new List<MetadataLinksChangedEventArgs>();
        tracker.Changed += (_, eventArgs) => raised.Add(eventArgs);
        var service = new MetadataCrossReferenceTransferService(
            links.Store,
            Mock.Of<IMetadataService>(),
            Mock.Of<IMetadataRefreshService>(),
            CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID),
            CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID),
            NullLogger<MetadataCrossReferenceTransferService>.Instance,
            tracker
        );
        var file = """
            AnidbAnimeId,AnidbEpisodeId,TmdbMovieId,Rating
            1,12,8,TitleMatches
            AnidbAnimeId,TmdbShowId,Rating
            4,9,DateMatches
            """;

        // The import's own reason wins over the caller's default.
        using (tracker.UseDefaultReason(MetadataLinkChangeReason.Manual))
            Assert.True((await service.Import(MetadataSource.TMDB, new StringReader(file), cancellationToken: TestContext.Current.CancellationToken)).Succeeded);

        var imported = Assert.Single(raised);
        Assert.Equal(MetadataLinkChangeReason.Import, imported.Reason);
        Assert.Equal(
            [(MetadataEntityType.Series, 4, "9"), (MetadataEntityType.Movie, 1, "8")],
            imported.Changes.Select(change => (change.EntityType, change.AnidbAnimeID, change.ProviderID!.ID))
        );
        Assert.All(imported.Changes, change => Assert.Equal(MetadataLinkChangeKind.Added, change.Kind));
    }

    [Fact]
    public async Task AnImportAddsUpdatesAndReplacesLinksAndRefreshesWhatIsMissing()
    {
        var harness = Tmdb();
        harness.Metadata.Setup(m => m.GetShokoSeriesByAnidbID(1)).Returns(Mock.Of<IShokoSeries>());
        var file = """
            # A comment.
            AnidbAnimeId,AnidbEpisodeId,TmdbMovieId,Rating
            1,12,8,TitleMatches
            AnidbAnimeId,TmdbShowId,Rating
            4,9,DateMatches
            4,0,UserVerified
            anidbanimeid,anidbepisodeid,tmdbshowid,tmdbepisodeid,rating
            1,10,5,55,UserVerified
            1,13,6,66,TitleMatches
            """;

        var result = await harness.Import(MetadataSource.TMDB, file);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.MoviesAdded);
        Assert.Equal(1, result.MoviesRemoved);
        Assert.Equal(2, result.SeriesAdded);
        Assert.Equal(1, result.EpisodesAdded);
        Assert.Equal(1, result.EpisodesUpdated);
        Assert.Equal(0, result.EpisodesRemoved);

        Assert.All(harness.Merged[0].Links, link => Assert.IsType<MetadataSeriesLinkData>(link));
        Assert.Equal(
            [(4, "9"), (1, "6")],
            harness.Merged[0].Links.Select(link => (link.AnidbAnimeID, link.ProviderID!.ID))
        );
        Assert.All(harness.Merged[0].Links, link => Assert.Equal(MatchRating.UserVerified, link.MatchRating));
        Assert.Equal("8", Assert.Single(harness.Merged[1].Links).ProviderID!.ID);
        Assert.Equal("7", Assert.Single(harness.Merged[1].Removals).ProviderID!.ID);
        Assert.Equal([("55", MatchRating.UserVerified), ("66", MatchRating.TitleMatches)],
            harness.Merged[2].Links.Select(link => (link.ProviderID!.ID, link.MatchRating)));

        // Only what anime 1, which is in the library, links to and is not
        // stored is refreshed.
        harness.Refresh.Verify(r => r.RefreshEntry(new(MetadataSource.TMDB, MetadataEntityType.Movie, "8"), false, It.IsAny<MetadataRefreshOptions?>(), false,
            false, It.IsAny<CancellationToken>()), Times.Once);
        harness.Refresh.Verify(r => r.RefreshEntry(new(MetadataSource.TMDB, MetadataEntityType.Series, "5"), false, It.IsAny<MetadataRefreshOptions?>(), false,
            false, It.IsAny<CancellationToken>()), Times.Once);
        harness.Refresh.Verify(r => r.RefreshEntry(new(MetadataSource.TMDB, MetadataEntityType.Series, "6"), false, It.IsAny<MetadataRefreshOptions?>(), false,
            false, It.IsAny<CancellationToken>()), Times.Once);
        harness.Refresh.Verify(r => r.RefreshEntry(new(MetadataSource.TMDB, MetadataEntityType.Series, "9"), It.IsAny<bool>(), It.IsAny<MetadataRefreshOptions?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(3, result.RefreshesQueued);
    }

    [Fact]
    public async Task AFileWithALineThatCanNotBeReadWritesNothing()
    {
        var harness = Tmdb();

        var result = await harness.Import(MetadataSource.TMDB, "AnidbAnimeId,TmdbShowId,Rating\n1,5,UserVerified\n0,5,UserVerified\n-1,6,UserVerified\n");
        var headless = await harness.Import(MetadataSource.TMDB, "1,5,UserVerified\n");
        var badRating = await harness.Import(MetadataSource.TMDB, "AnidbAnimeId,AnidbEpisodeId,TmdbMovieId,Rating\n1,2,0,UserVerified\n1,2,3,Nope\n");

        Assert.Equal([3, 4], result.Errors.Select(error => error.Line));
        Assert.Equal(1, Assert.Single(headless.Errors).Line);
        Assert.Equal([2, 3], badRating.Errors.Select(error => error.Line));
        Assert.Empty(harness.Merged);
    }

    [Fact]
    public async Task AnotherSourcesFileReadsItsOwnIDs()
    {
        var harness = new Harness();
        harness.Episodes.Add(new() { Source = TestSources.Plugin, AnidbAnimeID = 1, AnidbEpisodeID = 10, ProviderID = "old", ProviderParentID = "s" });
        var prefix = string.Concat(TestSources.Plugin.Value.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

        var result = await harness.Import(
            TestSources.Plugin,
            $"AnidbAnimeId,AnidbEpisodeId,{prefix}ShowId,{prefix}EpisodeId,Rating\n1,10,\"s,1\",e-1,UserVerified\n1,11,,,UserVerified\n"
        );

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.EpisodesAdded);
        Assert.Equal(1, result.EpisodesRemoved);
        var episodes = harness.Merged.Single(merge => merge.Links.FirstOrDefault() is MetadataEpisodeLinkData);
        var first = (MetadataEpisodeLinkData)episodes.Links[0];
        Assert.Equal(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "s,1"), first.ProviderParentID);
        Assert.Equal(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "e-1"), first.ProviderID);
        var second = (MetadataEpisodeLinkData)episodes.Links[1];
        Assert.Null(second.ProviderID);
        Assert.Null(second.ProviderParentID);
        Assert.Equal("old", Assert.Single(episodes.Removals).ProviderID!.ID);
    }

    [Fact]
    public async Task SectionsAreReadByTheirColumnsNotTheirNames()
    {
        var harness = new Harness();
        var stored = new Mock<IEpisode>();
        stored.SetupGet(e => e.SeasonID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Season, "21-1"));
        stored.SetupGet(e => e.SeasonNumber).Returns(1);
        stored.SetupGet(e => e.EpisodeNumber).Returns(3);
        harness.Metadata.Setup(m => m.GetEpisode(new(TestSources.Plugin, MetadataEntityType.Episode, "2100001"))).Returns(stored.Object);

        var result = await harness.Import(
            TestSources.Plugin,
            """
            AnidbAnimeId,SomeOtherAnimeId,Rating
            1,21,TitleMatches
            AnidbAnimeId,AnidbEpisodeId,SomeOtherAnimeId,SomeOtherEpisodeId,Rating
            1,10,21,2100001,DateMatches
            1,11,0,0,UserVerified
            """
        );

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.SeriesAdded);
        Assert.Equal(2, result.EpisodesAdded);
        var series = (MetadataSeriesLinkData)Assert.Single(harness.Merged[0].Links);
        Assert.Equal((1, new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "21")), (series.AnidbAnimeID, series.ProviderID));
        var episodes = harness.Merged[1].Links.Cast<MetadataEpisodeLinkData>().ToList();
        Assert.Equal(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "2100001"), episodes[0].ProviderID);
        Assert.Equal(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "21"), episodes[0].ProviderParentID);
        Assert.Equal(MatchRating.DateMatches, episodes[0].MatchRating);
        Assert.Equal(("21-1", 1, 3), (episodes[0].SeasonID!.ID, episodes[0].SeasonNumber, episodes[0].EpisodeNumber));

        // A 0 is a link to nothing, for every source.
        Assert.Null(episodes[1].ProviderID);
        Assert.Null(episodes[1].ProviderParentID);
    }

    [Fact]
    public async Task ASeriesLineOrEpisodeLineToNothingLinksNoSeries()
    {
        // Older TMDB files hold both; before the importer skipped them, each
        // left a show link with ID 0 behind.
        var tmdb = new Harness();
        var plugin = new Harness();
        var prefix = string.Concat(TestSources.Plugin.Value.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

        var tmdbResult = await tmdb.Import(
            MetadataSource.TMDB,
            "AnidbAnimeId,TmdbShowId,Rating\n4,0,UserVerified\nAnidbAnimeId,AnidbEpisodeId,TmdbShowId,TmdbEpisodeId,Rating\n4,40,0,0,UserVerified\n"
        );
        var pluginResult = await plugin.Import(
            TestSources.Plugin,
            $"AnidbAnimeId,{prefix}ShowId,Rating\n4,,UserVerified\n5,0,UserVerified\n" +
            $"AnidbAnimeId,AnidbEpisodeId,{prefix}ShowId,{prefix}EpisodeId,Rating\n4,40,,,UserVerified\n"
        );

        foreach (var (harness, result) in new[] { (tmdb, tmdbResult), (plugin, pluginResult) })
        {
            Assert.True(result.Succeeded);
            Assert.Equal(0, result.SeriesAdded);
            Assert.Equal(1, result.EpisodesAdded);
            Assert.DoesNotContain(harness.Merged, merge => merge.Links.Any(link => link is MetadataSeriesLinkData));
            Assert.Null(((MetadataEpisodeLinkData)Assert.Single(harness.Merged).Links.Single()).ProviderID);
        }
    }

    [Fact]
    public async Task AHeaderWithAnUnknownNumberOfColumnsIsRefused()
    {
        var harness = new Harness();

        var result = await harness.Import(TestSources.Plugin, "AnidbAnimeId,Rating\n1,UserVerified\n");

        Assert.Equal(1, Assert.Single(result.Errors).Line);
        Assert.Empty(harness.Merged);
    }

    #endregion
}
