using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
///   Looking stored releases up by the entries their cross-references name.
/// </summary>
public class StoredReleaseInfoLookupTests
{
    private static readonly MetadataGuid _movie = new(TestSources.Plugin, MetadataEntityType.Movie, "m1");

    private static StoredReleaseInfo Release(int id, params IReleaseVideoCrossReference[] crossReferences)
        => new() { StoredReleaseInfoID = id, ED2K = $"ED2K{id}", CrossReferences = crossReferences };

    private static StoredReleaseInfoRepository Build()
        => CachedRepo.Build<StoredReleaseInfoRepository, int, StoredReleaseInfo>(
            release => release.StoredReleaseInfoID,
            Release(1, ReleaseVideoCrossReference.ForAniDB(10, 1), ReleaseVideoCrossReference.ForAniDB(11, 1)),
            Release(2, ReleaseVideoCrossReference.ForAniDB(11, 1)),
            Release(3, ReleaseVideoCrossReference.For([_movie])),
            Release(4, ReleaseVideoCrossReference.For([_movie, new(MetadataSource.AniDB, MetadataEntityType.Episode, "12")], 0, 50))
        );

    [Fact]
    public void AnEntryOfAnySource_FindsEveryReleaseNamingIt_Once()
    {
        var releases = Build();

        Assert.Equal([3, 4], releases.GetByMetadataID(new(TestSources.Plugin, MetadataEntityType.Movie, "m1")).Select(release => release.StoredReleaseInfoID).Order());
        Assert.Equal([1, 2], releases.GetByMetadataID(new(MetadataSource.AniDB, MetadataEntityType.Series, "1")).Select(release => release.StoredReleaseInfoID).Order());
        Assert.Empty(releases.GetByMetadataID(new(TestSources.Plugin, MetadataEntityType.Movie, "m2")));
    }

    [Fact]
    public void TheAnidbLookups_ReadTheSameIndex()
    {
        var releases = Build();

        Assert.Equal([1, 2], releases.GetByAnidbEpisodeID(11).Select(release => release.StoredReleaseInfoID).Order());
        Assert.Equal([4], releases.GetByAnidbEpisodeID(12).Select(release => release.StoredReleaseInfoID));
        Assert.Equal([1, 2], releases.GetByAnidbAnimeID(1).Select(release => release.StoredReleaseInfoID).Order());
        Assert.Empty(releases.GetByAnidbEpisodeID(0));
    }
}
