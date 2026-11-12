using System.Linq;
using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Which entries of other sources than AniDB get refreshed when a release
///   links a video to them: the series of a series or episode, and the movie
///   itself.
/// </summary>
public class ReleaseMetadataRefreshTests
{
    [Theory]
    [InlineData("tmdb://series/2", "tmdb://series/2", "")]
    [InlineData("tmdb://episode/1,tmdb://series/2", "tmdb://series/2", "")]
    [InlineData("tmdb://episode/1", "tmdb://series/9", "")]
    [InlineData("tmdb://episode/7", "", "tmdb://episode/7")]
    [InlineData("tmdb://movie/3", "tmdb://movie/3", "")]
    [InlineData("anidb://episode/10,anidb://series/1", "", "")]
    [InlineData("anidb://episode/10,anidb://series/1,tmdb://episode/1,tmdb://series/2", "tmdb://series/2", "")]
    public void AReleaseRefreshesTheSeriesOrMovieOfEachEntryOfAnotherSource(string named, string refreshed, string unknownSeries)
    {
        var storedEpisode = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "1");
        var storedSeries = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "9");
        var episode = Mock.Of<IEpisode>(e => e.ID == storedEpisode && e.Series == Mock.Of<ISeries>(s => s.ID == storedSeries));
        IReleaseVideoCrossReference[] crossReferences = [ReleaseVideoCrossReference.For(Parse(named))];

        var targets = AnimeMetadataOrchestrator.GetRefreshTargets(crossReferences, id => id == storedEpisode ? episode : null, out var unknown);

        Assert.Equal(Parse(refreshed), targets);
        Assert.Equal(Parse(unknownSeries), unknown);
    }

    private static MetadataGuid[] Parse(string ids)
        => [.. ids.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(id => MetadataGuid.Parse(id))];
}
