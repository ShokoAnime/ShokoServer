using System;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers <see cref="MetadataCrossReferenceExtensions"/>, which keeps one
/// source's links or linked entries of an entry and, in the generic forms,
/// only those whose entry has the type asked for.
/// </summary>
public class MetadataCrossReferenceExtensionsTests
{
    #region Helpers

    /// <summary>
    /// A provider series type of the test's own, standing for a plugin's.
    /// </summary>
    public interface IPluginShow : ISeries;

    /// <summary>
    /// A provider episode type of the test's own, standing for a plugin's.
    /// </summary>
    public interface IPluginEpisode : IEpisode;

    private static IMetadataSeriesCrossReference SeriesLink(MetadataSource source, IMetadata? provider, int anidbAnimeID = 1, int ordering = 0)
    {
        var link = new Mock<IMetadataSeriesCrossReference>();
        link.SetupGet(xref => xref.Source).Returns(source);
        link.SetupGet(xref => xref.Provider).Returns(provider);
        link.SetupGet(xref => xref.AnidbAnimeID).Returns(anidbAnimeID);
        link.SetupGet(xref => xref.Ordering).Returns(ordering);
        link.SetupGet(xref => xref.ProviderID).Returns(provider?.ID);
        return link.Object;
    }

    private static ISeries Series(params IMetadataSeriesCrossReference[] links)
    {
        var series = new Mock<ISeries>();
        series.SetupGet(item => item.MetadataSeriesCrossReferences).Returns(links);
        return series.Object;
    }

    private static IPluginShow Show(string id)
    {
        var show = new Mock<IPluginShow>();
        show.SetupGet(item => item.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, id));
        return show.Object;
    }

    #endregion

    #region Source

    [Fact]
    public void OneSourcesLinksArePickedInOrder()
    {
        var first = SeriesLink(TestSources.Plugin, null);
        var other = SeriesLink(TestSources.LocalPlugin, null);
        var second = SeriesLink(TestSources.Plugin, null);
        var series = Series(first, other, second);

        Assert.Equal([first, second], series.GetSeriesCrossReferences(TestSources.Plugin));
        Assert.Equal([other], series.GetSeriesCrossReferences(TestSources.LocalPlugin));
        Assert.Empty(Series().GetSeriesCrossReferences(TestSources.Plugin));
    }

    [Fact]
    public void MissingArgumentsAreRefused()
    {
        var series = Series();

        Assert.Throws<ArgumentNullException>(() => series.GetSeriesCrossReferences(null!));
        Assert.Throws<ArgumentNullException>(() => series.GetSeriesCrossReferences<IPluginShow>(null!));
        Assert.Throws<ArgumentNullException>(() => ((ISeries)null!).GetSeriesCrossReferences(TestSources.Plugin));
        Assert.Throws<ArgumentNullException>(() => ((ISeries)null!).GetSeriesCrossReferences<IPluginShow>(TestSources.Plugin));
    }

    #endregion

    #region Typed

    [Fact]
    public void TypedLinksAndPlainLinksToTheTypeAreKept()
    {
        var typedShow = Show("1");
        var typed = new Mock<IMetadataSeriesCrossReference<IPluginShow>>();
        typed.SetupGet(xref => xref.Source).Returns(TestSources.Plugin);
        typed.SetupGet(xref => xref.Provider).Returns(typedShow);
        typed.As<IMetadataCrossReference>().SetupGet(xref => xref.Provider).Returns(typedShow);

        var plainShow = Show("2");
        var plain = SeriesLink(TestSources.Plugin, plainShow, anidbAnimeID: 3, ordering: 1);
        var series = Series(
            typed.Object,
            plain,
            SeriesLink(TestSources.Plugin, Mock.Of<ISeries>()),
            SeriesLink(TestSources.Plugin, null),
            SeriesLink(TestSources.LocalPlugin, Show("3"))
        );

        var links = series.GetSeriesCrossReferences<IPluginShow>(TestSources.Plugin);

        Assert.Equal(2, links.Count);
        Assert.Same(typed.Object, links[0]);
        Assert.Same(plainShow, links[1].Provider);
        Assert.Same(plainShow, ((IMetadataCrossReference)links[1]).Provider);
        Assert.Equal(plain.AnidbAnimeID, links[1].AnidbAnimeID);
        Assert.Equal(plain.Ordering, links[1].Ordering);
        Assert.Equal(plain.ProviderID, links[1].ProviderID);
        Assert.Equal(plain.Source, links[1].Source);
    }

    [Fact]
    public void AFileLinksEpisodeLinksAreTypedTheSameWay()
    {
        var episode = new Mock<IPluginEpisode>().Object;
        var plain = new Mock<IMetadataEpisodeCrossReference>();
        plain.SetupGet(xref => xref.Source).Returns(TestSources.Plugin);
        plain.SetupGet(xref => xref.Provider).Returns(episode);
        plain.SetupGet(xref => xref.AnidbEpisodeID).Returns(5);
        plain.SetupGet(xref => xref.SeasonID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Season, "6"));
        var unstored = new Mock<IMetadataEpisodeCrossReference>();
        unstored.SetupGet(xref => xref.Source).Returns(TestSources.Plugin);
        var video = new Mock<IVideoCrossReference>();
        video.SetupGet(item => item.MetadataEpisodeCrossReferences).Returns([plain.Object, unstored.Object]);

        var link = Assert.Single(video.Object.GetEpisodeCrossReferences<IPluginEpisode>(TestSources.Plugin));

        Assert.Same(episode, link.Provider);
        Assert.Equal(plain.Object.AnidbEpisodeID, link.AnidbEpisodeID);
        Assert.Equal(plain.Object.SeasonID, link.SeasonID);
        Assert.Equal(2, video.Object.GetEpisodeCrossReferences(TestSources.Plugin).Count);
    }

    #endregion

    #region Linked Entries

    [Fact]
    public void OneSourcesLinkedEntriesArePickedInOrder()
    {
        var first = Show("1");
        var other = new Mock<ISeries>();
        other.SetupGet(item => item.ID).Returns(new MetadataGuid(TestSources.LocalPlugin, MetadataEntityType.Series, "2"));
        var second = Show("3");
        var series = new Mock<IShokoSeries>();
        series.SetupGet(item => item.LinkedSeries).Returns([first, other.Object, second]);

        Assert.Equal([first, second], series.Object.GetLinkedSeries(TestSources.Plugin));
        Assert.Equal([other.Object], series.Object.GetLinkedSeries(TestSources.LocalPlugin));
        Assert.Throws<ArgumentNullException>(() => series.Object.GetLinkedSeries(null!));
        Assert.Throws<ArgumentNullException>(() => ((IShokoSeries)null!).GetLinkedSeries<IPluginShow>(TestSources.Plugin));
    }

    [Fact]
    public void TypedLinkedEntriesKeepOnlyTheType()
    {
        var show = Show("1");
        var plain = new Mock<ISeries>();
        plain.SetupGet(item => item.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "2"));
        var series = new Mock<IShokoSeries>();
        series.SetupGet(item => item.LinkedSeries).Returns([plain.Object, show]);

        Assert.Equal([show], series.Object.GetLinkedSeries<IPluginShow>(TestSources.Plugin));
        Assert.Empty(series.Object.GetLinkedSeries<IPluginShow>(TestSources.LocalPlugin));
    }

    #endregion
}
