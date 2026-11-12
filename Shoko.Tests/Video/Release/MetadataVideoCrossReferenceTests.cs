using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
///   The direct links a stored release makes between its video and the
///   entries its cross-references name.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataVideoCrossReferenceTests
{
    private static readonly MetadataGuid _movie = new(TestSources.Plugin, MetadataEntityType.Movie, "m1");

    private static readonly MetadataGuid _episode = new(MetadataSource.AniDB, MetadataEntityType.Episode, "10");

    private static readonly MetadataGuid _anime = new(MetadataSource.AniDB, MetadataEntityType.Series, "1");

    private static StoredReleaseInfo Release()
        => new()
        {
            StoredReleaseInfoID = 1,
            ED2K = "AAAA",
            FileSize = 1,
            CrossReferences = [ReleaseVideoCrossReference.ForAniDB(10, 1, 0, 50), ReleaseVideoCrossReference.For([_movie])],
        };

    private static RepoFactoryScope Scope()
        => new RepoFactoryScope()
            .With<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID, [new() { VideoLocalID = 5, Hash = "AAAA", FileSize = 1 }])
            .With<AnimeEpisodeRepository, int, AnimeEpisode>(episode => episode.AnimeEpisodeID, [new() { AnimeEpisodeID = 20, AnimeSeriesID = 30, AniDB_EpisodeID = 10 }])
            .With<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID, [new() { AnimeSeriesID = 30, AniDB_ID = 1 }])
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, [])
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID, []);

    [Fact]
    public void AReleaseMakesOneLinkPerCrossReference_ToItsMainEntry()
    {
        using var scope = Scope();
        var release = Release();

        var links = MetadataVideoCrossReference.For(release).ToList();

        Assert.Equal([_episode, _movie], links.Select(link => link.ID));
        Assert.All(links, link => Assert.Equal(5, link.Video!.LocalID));
        Assert.Equal([(0, 50, 2), (0, 100, 1)], links.Select(link => (link.PercentageStart, link.PercentageEnd, link.PercentageGroupSize)));
        Assert.Equal(release.CrossReferences[1].IDs, links[1].IDs);
        Assert.Equal([_episode, _anime], links[0].IDs);
        Assert.Equal([true, false], links.Select(link => link.IsAnidb));
    }

    [Theory]
    [InlineData("anidb://series/1")]
    [InlineData("anidb://episode/10")]
    public void AskingForAnyIDOfACrossReference_GivesItsMainEntrysLink(string asked)
    {
        using var scope = Scope();

        var link = Assert.Single(MetadataVideoCrossReference.For(Release(), MetadataGuid.Parse(asked)));

        Assert.Equal(_episode, link.ID);
    }

    [Fact]
    public void AnAnidbLink_ReachesItsShokoEpisodeAndSeries_AndAnotherSourcesLinkNeedsItsEntry()
    {
        using var scope = Scope();
        var links = MetadataVideoCrossReference.For(Release()).ToList();

        Assert.Equal(20, links[0].ShokoEpisode!.ID.GetNumericID<int>());
        Assert.Equal(30, links[0].ShokoSeries!.ID.GetNumericID<int>());
        Assert.Equal([30], links[0].LinkedShokoSeries.Select(series => series.ID.GetNumericID<int>()));
        Assert.Null(links[1].ShokoSeries);
        Assert.Empty(links[1].LinkedShokoEpisodes);
    }

    [Theory]
    [InlineData("anidb://series/1,anidb://episode/10", "anidb://episode/10,anidb://series/1")]
    [InlineData("test-plugin://episode/e1,anidb://episode/10", "anidb://episode/10,test-plugin://episode/e1")]
    [InlineData("test-plugin://series/s1,test-plugin://episode/e1", "test-plugin://episode/e1,test-plugin://series/s1")]
    public void AStoredCrossReferenceKeepsItsMainEntryFirst(string given, string stored)
    {
        using var scope = Scope();
        var ids = given.Split(',').Select(id => MetadataGuid.Parse(id)).ToList();

        var xref = new EmbeddedCrossReference(ReleaseVideoCrossReference.For(ids));

        Assert.Equal(stored, string.Join(",", xref.IDs));
        Assert.Equal(stored.Split(',')[0], ReleaseVideoCrossReference.For(ids).MainID!.ToString());
    }
}
