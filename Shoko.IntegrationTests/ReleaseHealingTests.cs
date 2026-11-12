using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Xunit;

#pragma warning disable CS0618
namespace Shoko.IntegrationTests;

/// <summary>
/// Heals a stored release linking an AniDB episode without its anime through
/// the core's AniDB validator, and checks the release and its AniDB link rows
/// stay in step on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ReleaseHealingTests(DatabaseMigrationFixture fixture)
{
    private const int AnimeID = 9_870_501;

    private const int EpisodeID = 9_870_502;

    [Fact]
    public async System.Threading.Tasks.Task AHeal_FillsInTheAnime_OnTheReleaseAndItsLinkRows()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var videos = fixture.Services.GetRequiredService<VideoLocalRepository>();
        var releases = fixture.Services.GetRequiredService<StoredReleaseInfoRepository>();
        var links = fixture.Services.GetRequiredService<CrossRef_File_EpisodeRepository>();
        var episodes = fixture.Services.GetRequiredService<AniDB_EpisodeRepository>();
        var service = (VideoReleaseService)fixture.Services.GetRequiredService<IVideoReleaseService>();

        var video = new VideoLocal
        {
            Hash = $"{Guid.NewGuid():N}".ToUpperInvariant(),
            FileSize = 734_003_201,
            FileName = "video.mkv",
            DateTimeCreated = DateTime.Now,
            DateTimeUpdated = DateTime.Now,
        };
        videos.Save(video, false);
        var episode = new AniDB_Episode
        {
            EpisodeID = EpisodeID,
            AnimeID = AnimeID,
            EpisodeType = EpisodeType.Episode,
            EpisodeNumber = 1,
            Description = string.Empty,
            DateTimeUpdated = DateTime.Now,
        };
        episodes.Save(episode);
        var release = new StoredReleaseInfo
        {
            ED2K = video.Hash,
            FileSize = video.FileSize,
            ProviderName = "User",
            CrossReferences = [ReleaseVideoCrossReference.ForAniDB(EpisodeID)],
            CreatedAt = DateTime.Now,
            LastUpdatedAt = DateTime.Now,
        };
        releases.Save(release);
        links.Save(new CrossRef_File_Episode { Hash = video.Hash, FileSize = video.FileSize, EpisodeID = EpisodeID, AnimeID = 0, Percentage = 100 });
        try
        {
            Assert.True(await service.HealRelease(release, TestContext.Current.CancellationToken));

            var stored = releases.GetByEd2kAndFileSize(video.Hash, video.FileSize)!;
            Assert.Equal([new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, EpisodeID.ToString()), new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, AnimeID.ToString())], Assert.Single(stored.CrossReferences).IDs);
            Assert.Equal((EpisodeID, AnimeID), links.GetByEd2k(video.Hash).Select(link => (link.EpisodeID, link.AnimeID)).Single());

            // A second heal finds nothing more to fill in.
            Assert.False(await service.HealRelease(stored, TestContext.Current.CancellationToken));
        }
        finally
        {
            links.Delete(links.GetByEd2k(video.Hash).ToList());
            if (releases.GetByEd2kAndFileSize(video.Hash, video.FileSize) is { } row)
                releases.Delete(row);
            episodes.Delete(episodes.GetByAnimeID(AnimeID));
            videos.Delete(video);
        }
    }
}
