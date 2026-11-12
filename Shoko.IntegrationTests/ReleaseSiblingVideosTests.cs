using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Xunit;

#pragma warning disable CS0618
namespace Shoko.IntegrationTests;

/// <summary>
/// The videos a new file takes its watched state from: those of the same
/// episode or movie of another source, found through the releases naming it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ReleaseSiblingVideosTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void TheVideosOfTheSameEpisodeOfAnotherSource_AreFoundThroughTheirReleases()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var videos = fixture.Services.GetRequiredService<VideoLocalRepository>();
        var releases = fixture.Services.GetRequiredService<StoredReleaseInfoRepository>();
        var service = (VideoReleaseService)fixture.Services.GetRequiredService<IVideoReleaseService>();
        var episode = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "9870601");
        var otherEpisode = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "9870602");

        var watched = Video();
        var incoming = Video();
        var unrelated = Video();
        var stored = new[] { Release(watched, episode), Release(incoming, episode), Release(unrelated, otherEpisode) };
        releases.Save(stored);
        try
        {
            var siblings = service.GetVideosOfTheSameEntries(incoming, stored[1]);

            Assert.Equal([watched.Hash], siblings.Select(video => video.ED2K));
        }
        finally
        {
            releases.Delete(stored.Select(release => releases.GetByEd2kAndFileSize(release.ED2K, release.FileSize)).OfType<StoredReleaseInfo>().ToList());
            videos.Delete(watched);
            videos.Delete(incoming);
            videos.Delete(unrelated);
        }

        VideoLocal Video()
        {
            var video = new VideoLocal
            {
                Hash = $"{Guid.NewGuid():N}".ToUpperInvariant(),
                FileSize = 734_003_202,
                FileName = "video.mkv",
                DateTimeCreated = DateTime.Now,
                DateTimeUpdated = DateTime.Now,
            };
            videos.Save(video, false);
            return video;
        }

        static StoredReleaseInfo Release(VideoLocal video, MetadataGuid id)
            => new()
            {
                ED2K = video.Hash,
                FileSize = video.FileSize,
                ProviderName = "User",
                CrossReferences = [ReleaseVideoCrossReference.For([id])],
                CreatedAt = DateTime.Now,
                LastUpdatedAt = DateTime.Now,
            };
    }
}
