using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Repositories;

/// <summary>
///   Which videos the sweeps take as unrecognised: those with neither an
///   AniDB link nor a stored release of any source, provisional or not.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class RecognisedVideoTests
{
    [Fact]
    public void AVideoWithAReleaseOfAnySource_IsRecognised_ProvisionalOrNot_AndAUserReleaseIsManual()
    {
        var plugin = ReleaseVideoCrossReference.For([new MetadataGuid(TestSources.Plugin, MetadataEntityType.Movie, "m1")]);
        using var scope = new RepoFactoryScope()
            .With<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID,
            [
                new() { VideoLocalID = 1, Hash = "AAAA", FileSize = 1 },
                new() { VideoLocalID = 2, Hash = "BBBB", FileSize = 2 },
                new() { VideoLocalID = 3, Hash = "CCCC", FileSize = 3 },
                new() { VideoLocalID = 4, Hash = "DDDD", FileSize = 4 },
                new() { VideoLocalID = 5, Hash = "EEEE", FileSize = 5 },
            ])
            .With<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(xref => xref.CrossRef_File_EpisodeID,
            [
                new() { CrossRef_File_EpisodeID = 1, Hash = "AAAA", FileSize = 1, EpisodeID = 10, AnimeID = 1 },
            ])
            .With<StoredReleaseInfoRepository, int, StoredReleaseInfo>(release => release.StoredReleaseInfoID,
            [
                new() { StoredReleaseInfoID = 1, ED2K = "AAAA", FileSize = 1, CrossReferences = [ReleaseVideoCrossReference.ForAniDB(10, 1)] },
                new() { StoredReleaseInfoID = 2, ED2K = "BBBB", FileSize = 2, CrossReferences = [plugin] },
                new() { StoredReleaseInfoID = 3, ED2K = "CCCC", FileSize = 3, CrossReferences = [plugin], DeferToNext = true },
                new() { StoredReleaseInfoID = 4, ED2K = "EEEE", FileSize = 5, CrossReferences = [plugin], ProviderName = "User" },
            ]);

        var unrecognised = RepoFactory.VideoLocal.GetVideosWithoutEpisodeUnsorted();

        Assert.Equal([4], unrecognised.Select(video => video.VideoLocalID));
        Assert.Equal([5], RepoFactory.VideoLocal.GetManuallyLinkedVideos().Select(video => video.VideoLocalID));
    }

    [Fact]
    public void ABrokenLinksSweep_TakesAVideoLinkedOnlyToAnotherSource_WhileItsEntryIsMissing()
    {
        var tmdb = ReleaseVideoCrossReference.For([new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "1")]);
        using var scope = new RepoFactoryScope()
            .With<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID,
            [
                new() { VideoLocalID = 1, Hash = "AAAA", FileSize = 1 },
                new() { VideoLocalID = 2, Hash = "BBBB", FileSize = 2 },
            ])
            .With<VideoLocal_PlaceRepository, int, VideoLocal_Place>(place => place.ID, [])
            .With<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(xref => xref.CrossRef_File_EpisodeID, [])
            .With<StoredReleaseInfoRepository, int, StoredReleaseInfo>(release => release.StoredReleaseInfoID,
            [
                new() { StoredReleaseInfoID = 1, ED2K = "BBBB", FileSize = 2, CrossReferences = [tmdb] },
            ]);

        // Nothing resolves the TMDB entry here, so video 2 is in import limbo.
        Assert.Equal([1], RepoFactory.VideoLocal.GetVideosWithoutEpisode().Select(video => video.VideoLocalID));
        Assert.Equal([1, 2], RepoFactory.VideoLocal.GetVideosWithoutEpisode(includeBrokenXRefs: true).Select(video => video.VideoLocalID).Order());
    }

    [Theory]
    [InlineData(new[] { FileIncludeOnlyType.Unrecognized }, new FileExcludeTypes[0], new[] { 1 })]
    [InlineData(new FileIncludeOnlyType[0], new[] { FileExcludeTypes.Unrecognized }, new[] { 2, 3 })]
    [InlineData(new[] { FileIncludeOnlyType.ManualLinks }, new FileExcludeTypes[0], new[] { 2 })]
    [InlineData(new FileIncludeOnlyType[0], new[] { FileExcludeTypes.ManualLinks }, new[] { 1, 3 })]
    public void TheFileFilters_TakeAReleaseOfAnySourceAsRecognised_AndAUserReleaseAsManual(FileIncludeOnlyType[] includeOnly, FileExcludeTypes[] exclude, int[] expected)
    {
        var tmdb = ReleaseVideoCrossReference.For([new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "1")]);
        using var scope = new RepoFactoryScope()
            .With<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID,
            [
                new() { VideoLocalID = 1, Hash = "AAAA", FileSize = 1 },
                new() { VideoLocalID = 2, Hash = "BBBB", FileSize = 2 },
                new() { VideoLocalID = 3, Hash = "CCCC", FileSize = 3 },
            ])
            .With<VideoLocal_PlaceRepository, int, VideoLocal_Place>(place => place.ID, [])
            .With<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(xref => xref.CrossRef_File_EpisodeID, [])
            .With<StoredReleaseInfoRepository, int, StoredReleaseInfo>(release => release.StoredReleaseInfoID,
            [
                new() { StoredReleaseInfoID = 1, ED2K = "BBBB", FileSize = 2, CrossReferences = [tmdb], ProviderName = "User" },
                new() { StoredReleaseInfoID = 2, ED2K = "CCCC", FileSize = 3, CrossReferences = [tmdb], ProviderName = "Example" },
            ]);
        var videos = RepoFactory.VideoLocal.GetAll();

        // Nothing resolves the TMDB entry here, so the recognised files are in import limbo: include those.
        // A page past the end gives the total without building the file models.
        var kept = videos
            .Where(video => ModelHelper.FilterFiles([video], new JMMUser(), 1, 100, [FileNonDefaultIncludeType.ImportLimbo], exclude, includeOnly, null, null, skipSort: true).Total is 1)
            .Select(video => video.VideoLocalID)
            .Order();

        Assert.Equal(expected, kept);
    }
}
