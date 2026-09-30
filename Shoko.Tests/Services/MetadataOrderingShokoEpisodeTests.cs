using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers hiding a Shoko episode through the ordering service, which goes
/// through the episode's own <see cref="AnimeEpisode.IsHidden"/> flag rather
/// than the hidden episodes table.
/// </summary>
/// <remarks>
/// The episode reads its series through the <c>RepoFactory</c> statics, so
/// an empty series table is installed there, which leaves the series and
/// group stats alone.
/// </remarks>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataOrderingShokoEpisodeTests
{
    #region Hiding

    [Fact]
    public void HidingAShokoEpisodeSetsItsOwnFlagAndSavesIt()
    {
        using var scope = new RepoFactoryScope().With<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID);
        var episode = new AnimeEpisode { AnimeEpisodeID = 5, AnimeSeriesID = 77 };
        var tables = new OrderingTables(episode);
        var service = tables.Build(() => Mock.Of<IMetadataService>());
        var episodeID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, "5");

        Assert.True(service.SetEpisodeHidden(episodeID, true));
        Assert.True(episode.IsHidden);
        Assert.True(service.IsEpisodeHidden(episodeID));
        tables.ShokoEpisodes.Verify(repository => repository.Save(episode), Times.Once);

        Assert.False(service.SetEpisodeHidden(episodeID, true));
        tables.ShokoEpisodes.Verify(repository => repository.Save(episode), Times.Once);

        Assert.True(service.SetEpisodeHidden(episodeID, false));
        Assert.False(episode.IsHidden);
        Assert.False(service.IsEpisodeHidden(episodeID));
        tables.ShokoEpisodes.Verify(repository => repository.Save(episode), Times.Exactly(2));
        Assert.Empty(tables.RowState.Hidden);
    }

    #endregion
}
