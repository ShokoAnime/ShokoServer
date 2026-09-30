using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// The numeric-ID lookups build the entry's <see cref="MetadataGuid"/> and hand it to the
/// matching <see cref="IMetadataService"/> lookup.
/// </summary>
public class MetadataServiceExtensionsTests
{
    [Fact]
    public void EachLookupAsksForItsOwnKind()
    {
        var series = Mock.Of<ISeries>();
        var episode = Mock.Of<IEpisode>();
        var movie = Mock.Of<IMovie>();
        var season = Mock.Of<ISeason>();
        var collection = Mock.Of<ICollection>();
        var studio = Mock.Of<IMetadata>();
        var service = new Mock<IMetadataService>(MockBehavior.Strict);
        service.Setup(s => s.GetSeries(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "314"))).Returns(series);
        service.Setup(s => s.GetEpisode(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "80"))).Returns(episode);
        service.Setup(s => s.GetMovie(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "81"))).Returns(movie);
        service.Setup(s => s.GetSeason(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Season, "82"))).Returns(season);
        service.Setup(s => s.GetCollection(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Collection, "83"))).Returns(collection);
        service.Setup(s => s.GetEntry<IMetadata>(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Studio, "7"))).Returns(studio);

        Assert.Same(series, service.Object.GetSeries(MetadataSource.AniDB, 314));
        Assert.Same(episode, service.Object.GetEpisode(MetadataSource.TMDB, 80));
        Assert.Same(movie, service.Object.GetMovie(MetadataSource.TMDB, 81));
        Assert.Same(season, service.Object.GetSeason(MetadataSource.TMDB, 82));
        Assert.Same(collection, service.Object.GetCollection(MetadataSource.TMDB, 83));
        Assert.Same(studio, service.Object.GetEntry<IMetadata>(MetadataSource.TMDB, MetadataEntityType.Studio, 7));
    }
}
