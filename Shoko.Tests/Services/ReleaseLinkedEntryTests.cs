using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   An entry a stored release links a video to directly is in use, so the
///   purge and the refresh keep it, even with no AniDB link to it.
/// </summary>
public class ReleaseLinkedEntryTests
{
    private static readonly MetadataGuid _movie = new(MetadataSource.TMDB, MetadataEntityType.Movie, "1");

    private static readonly MetadataGuid _series = new(MetadataSource.TMDB, MetadataEntityType.Series, "2");

    private static readonly MetadataGuid _episode = new(MetadataSource.TMDB, MetadataEntityType.Episode, "3");

    [Theory]
    [InlineData("tmdb://movie/1", "tmdb://movie/1", true)]
    [InlineData("tmdb://episode/3", "tmdb://series/2", true)]
    [InlineData("tmdb://movie/9", "tmdb://movie/1", false)]
    [InlineData("tmdb://movie/9", "tmdb://series/2", false)]
    public void AnEntryARelease_OrOneOfItsEpisodes_Names_IsLinked(string named, string entry, bool linked)
    {
        var releases = CachedRepo.Build<StoredReleaseInfoRepository, int, StoredReleaseInfo>(release => release.StoredReleaseInfoID,
        [
            new() { StoredReleaseInfoID = 1, ED2K = "AAAA", FileSize = 1, CrossReferences = [ReleaseVideoCrossReference.For([MetadataGuid.Parse(named)])] },
        ]);
        var store = new Mock<IMetadataCrossReferenceStore>();
        store.Setup(s => s.GetLinksTo(It.IsAny<MetadataGuid>())).Returns([]);
        store.Setup(s => s.GetEpisodeLinksInto(It.IsAny<MetadataGuid>())).Returns([]);
        var episode = Mock.Of<IEpisode>(e => e.ID == _episode);
        var series = Mock.Of<ISeries>(s => s.ID == _series && s.Episodes == new[] { episode });
        var metadata = new Mock<IMetadataService>();
        metadata.Setup(m => m.GetEntry(_series)).Returns(series);

        Assert.Equal(linked, store.Object.IsLinked(releases, metadata.Object, MetadataGuid.Parse(entry)));
    }
}
