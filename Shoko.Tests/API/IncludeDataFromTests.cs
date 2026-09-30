using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers <c>includeDataFrom</c> on the series and episode responses: every
/// value it took before still means the same source, any registered source
/// can be asked for, and a plugin source adds a generic block under
/// <c>Sources</c> while AniDB and TMDB keep their own.
/// </summary>
public class IncludeDataFromTests
{
    #region Binding

    [Theory]
    [InlineData("AniDB", "anidb")]
    [InlineData("anidb", "anidb")]
    [InlineData("0", "anidb")]
    [InlineData("TMDB", "tmdb")]
    [InlineData("tmdb", "tmdb")]
    [InlineData("1", "tmdb")]
    [InlineData("AniList", "anilist")]
    [InlineData("anilist", "anilist")]
    [InlineData("3", "anilist")]
    [InlineData("test-plugin", "test-plugin")]
    [InlineData("TEST_PLUGIN", "test-plugin")]
    public void AnOldValue_OrARegisteredSourcesValueOrAlias_NamesItsSource(string text, string value)
    {
        MetadataSource[] sources = [MetadataSource.AniDB, MetadataSource.TMDB, TestSources.AniList, TestSources.Plugin];

        Assert.Equal([sources.Single(source => source.Value == value)], MetadataSourceSetModelBinder.Parse([text]));
    }

    [Fact]
    public void CommaSeparatedAndRepeatedValuesAreBothRead()
    {
        Assert.Equal(new HashSet<MetadataSource> { MetadataSource.AniDB, MetadataSource.TMDB }, MetadataSourceSetModelBinder.Parse(["AniDB,TMDB"]));
        Assert.Equal(new HashSet<MetadataSource> { MetadataSource.AniDB, MetadataSource.TMDB }, MetadataSourceSetModelBinder.Parse(["AniDB", " TMDB "]));
        Assert.Equal(new HashSet<MetadataSource> { MetadataSource.AniDB }, MetadataSourceSetModelBinder.Parse(["AniDB,anidb", "0"]));
    }

    [Theory]
    [InlineData("nothing-registered")]
    [InlineData("2")]
    [InlineData("")]
    public void TextNamingNoRegisteredSourceIsLeftOut(string text)
        => Assert.Empty(MetadataSourceSetModelBinder.Parse([text, null]));

    #endregion

    #region The generic block

    [Fact]
    public void OnlyPluginSourcesGetTheGenericBlock()
    {
        Assert.Empty(LinkedMetadataHelper.GenericSources(null));
        Assert.Empty(LinkedMetadataHelper.GenericSources(new HashSet<MetadataSource> { MetadataSource.AniDB, MetadataSource.TMDB }));
        // The other core sources hold no links, so they add no block, as before they were accepted.
        Assert.Empty(LinkedMetadataHelper.GenericSources(new HashSet<MetadataSource> { MetadataSource.Shoko, MetadataSource.User, MetadataSource.Generated }));
        Assert.Equal(
            [TestSources.AniList, TestSources.Plugin],
            LinkedMetadataHelper.GenericSources(new HashSet<MetadataSource> { TestSources.Plugin, MetadataSource.TMDB, TestSources.AniList })
        );
    }

    private static MetadataGuid Guid(MetadataEntityType type, string id)
        => new(TestSources.Plugin, type, id);

    private static TLink Link<TLink>(MetadataGuid? providerID) where TLink : class, IMetadataCrossReference
    {
        var link = new Mock<TLink>();
        link.SetupGet(l => l.Source).Returns(TestSources.Plugin);
        link.SetupGet(l => l.ProviderID).Returns(providerID);
        return link.Object;
    }

    private static Mock<TEntry> Entry<TEntry>(MetadataGuid id, string title) where TEntry : class, IMetadata
    {
        var entry = new Mock<TEntry>();
        var titleMock = new Mock<ITitle>();
        titleMock.SetupGet(t => t.Value).Returns(title);
        titleMock.SetupGet(t => t.Language).Returns(TitleLanguage.English);
        titleMock.SetupGet(t => t.LanguageCode).Returns("en");
        titleMock.SetupGet(t => t.Type).Returns(TitleType.Main);
        var titled = entry.As<IWithTitles>();
        titled.SetupGet(t => t.Title).Returns(title);
        titled.SetupGet(t => t.Titles).Returns([titleMock.Object]);
        titled.SetupGet(t => t.DefaultTitle).Returns(titleMock.Object);
        titled.SetupGet(t => t.PreferredTitle).Returns(titleMock.Object);
        var descriptionMock = new Mock<IText>();
        descriptionMock.SetupGet(d => d.Value).Returns($"About {title}.");
        entry.As<IWithOverviews>().SetupGet(d => d.PreferredOverview).Returns(descriptionMock.Object);
        entry.As<IWithImages>()
            .Setup(i => i.GetImages(It.IsAny<ImageFilteringOptions?>()))
            .Returns(Array.Empty<IImage>());
        entry.SetupGet(e => e.ID).Returns(id);
        entry.SetupGet(e => e.Source).Returns(id.Source);
        entry.SetupGet(e => e.EntityType).Returns(id.EntityType);
        return entry;
    }

    [Fact]
    public void ASeriesBlockHoldsTheLinkedSeriesAndMoviesOnce()
    {
        var show = Guid(MetadataEntityType.Series, "show-1");
        var film = Guid(MetadataEntityType.Movie, "film-1");
        var gone = Guid(MetadataEntityType.Series, "not-stored");
        var series = Entry<ISeries>(show, "Show One");
        var movie = Entry<IMovie>(film, "Film One");

        var metadataService = new Mock<IMetadataService>();
        metadataService.Setup(s => s.GetSeriesCrossReferences(20, TestSources.Plugin))
            .Returns([Link<IMetadataSeriesCrossReference>(show), Link<IMetadataSeriesCrossReference>(null), Link<IMetadataSeriesCrossReference>(gone)]);
        metadataService.Setup(s => s.GetMovieCrossReferencesForSeries(20, TestSources.Plugin))
            .Returns([Link<IMetadataMovieCrossReference>(film), Link<IMetadataMovieCrossReference>(film)]);
        metadataService.Setup(s => s.GetSeriesCrossReferences(20, TestSources.AniList)).Returns([]);
        metadataService.Setup(s => s.GetMovieCrossReferencesForSeries(20, TestSources.AniList)).Returns([]);
        metadataService.Setup(s => s.GetEntry(show)).Returns(series.Object);
        metadataService.Setup(s => s.GetEntry(film)).Returns(movie.Object);

        var blocks = LinkedMetadataHelper.ForSeries(metadataService.Object, 20, [TestSources.Plugin, TestSources.AniList]);

        // Each link once, leaving out the ones naming no entry or an entry that is not stored.
        var block = blocks[TestSources.Plugin];
        var linkedSeries = Assert.Single(block.Series);
        Assert.Equal("show-1", linkedSeries.ID);
        Assert.Equal("Show One", linkedSeries.Title);
        Assert.Equal("film-1", Assert.Single(block.Movies).ID);

        Assert.Empty(blocks[TestSources.AniList].Series);
        Assert.Empty(blocks[TestSources.AniList].Movies);
    }

    [Fact]
    public void AnEpisodeBlockHoldsTheLinkedEpisodesAndMovies()
    {
        var episodeID = Guid(MetadataEntityType.Episode, "ep-1");
        var film = Guid(MetadataEntityType.Movie, "film-1");
        var episode = Entry<IEpisode>(episodeID, "Episode One");
        episode.SetupGet(e => e.AirDate).Returns(new DateOnly(2020, 4, 5));
        episode.SetupGet(e => e.SeasonNumber).Returns(1);
        episode.SetupGet(e => e.EpisodeNumber).Returns(3);
        var movie = Entry<IMovie>(film, "Film One");

        var metadataService = new Mock<IMetadataService>();
        metadataService.Setup(s => s.GetEpisodeCrossReferences(201, TestSources.Plugin)).Returns([Link<IMetadataEpisodeCrossReference>(episodeID)]);
        metadataService.Setup(s => s.GetMovieCrossReferences(201, TestSources.Plugin)).Returns([Link<IMetadataMovieCrossReference>(film)]);
        metadataService.Setup(s => s.GetEntry(episodeID)).Returns(episode.Object);
        metadataService.Setup(s => s.GetEntry(film)).Returns(movie.Object);

        var block = LinkedMetadataHelper.ForEpisode(metadataService.Object, 201, [TestSources.Plugin])[TestSources.Plugin];

        var linked = Assert.Single(block.Episodes);
        Assert.Equal("ep-1", linked.ID);
        Assert.Equal(new PartialDateOnly(2020, 4, 5), linked.AirDate);
        Assert.Equal(1, linked.SeasonNumber);
        Assert.Equal(3, linked.EpisodeNumber);
        Assert.Null(linked.EndDate);
        Assert.Equal("film-1", Assert.Single(block.Movies).ID);
    }

    #endregion
}
