using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.Infrastructure.TestViewers;

namespace Shoko.Tests.API;

/// <summary>
/// Covers who the file feed sends a file's events to, by the anime the file
/// is linked to and the restricted tags of each user.
/// </summary>
public class FileEventEmitterTests
{
    [Theory]
    [InlineData(new[] { HiddenAnimeID }, false)]
    [InlineData(new[] { HiddenAnimeID, VisibleAnimeID }, true)]
    [InlineData(new int[0], true)]
    [InlineData(new[] { UnknownAnimeID }, true)]
    public async Task Hashed_ReachesTheRestrictedUserOnlyWhenTheFileIsVisible(int[] animeIDs, bool restrictedReceives)
    {
        var harness = await Harness.Create();
        var video = Video(animeIDs);

        harness.VideoService.Raise(service => service.VideoFileHashed += null, new VideoFileHashedEventArgs("/file.mkv", Folder(), File(), video)
        {
            UsedExistingHashes = false,
            IsNewVideo = true,
            IsNewFile = true,
            Hashes = [],
        });

        Assert.Equal(Expected(restrictedReceives), harness.Hub.ReceivedBy("file:hashed"));
    }

    [Theory]
    [InlineData(new int[0], new[] { HiddenAnimeID }, false)]
    [InlineData(new[] { UnknownAnimeID }, new int[0], false)]
    [InlineData(new int[0], new[] { HiddenAnimeID, VisibleAnimeID }, true)]
    [InlineData(new int[0], new int[0], true)]
    public async Task Deleted_IsCheckedAgainstTheCapturedSeriesAndHidesUnknownAnime(int[] videoAnimeIDs, int[] seriesAnimeIDs, bool restrictedReceives)
    {
        var harness = await Harness.Create();
        var series = seriesAnimeIDs.Select(animeID => Mock.Of<IShokoSeries>(s => s.AnidbAnimeID == animeID)).ToArray();

        harness.VideoService.Raise(service => service.VideoFileDeleted += null, new VideoFileEventArgs("/file.mkv", Folder(), File(), Video(videoAnimeIDs), [], series, []));

        Assert.Equal(Expected(restrictedReceives), harness.Hub.ReceivedBy("file:deleted"));
    }

    private static string[] Expected(bool restrictedReceives)
        => restrictedReceives ? [RestrictedConnection, UnrestrictedConnection] : [UnrestrictedConnection];

    private static IVideo Video(int[] animeIDs)
    {
        var video = new Mock<IVideo>();
        video.Setup(v => v.CrossReferences).Returns([.. animeIDs.Select(animeID => Mock.Of<IVideoCrossReference>(xref => xref.AnidbAnimeID == animeID))]);
        video.Setup(v => v.Episodes).Returns([]);
        video.Setup(v => v.Series).Returns([]);
        video.Setup(v => v.Groups).Returns([]);
        return video.Object;
    }

    private static IManagedFolder Folder()
        => Mock.Of<IManagedFolder>(folder => folder.Path == "/library");

    private static IVideoFile File()
        => Mock.Of<IVideoFile>(file => file.Path == "/library/file.mkv");

    private sealed class Harness
    {
        public RecordingHub Hub { get; } = new();

        public Mock<IVideoService> VideoService { get; } = new();

        public FileEventEmitter Emitter { get; }

        private Harness()
            => Emitter = new(Hub.Typed, VideoService.Object, AnimeRepository(), NullLogger<FileEventEmitter>.Instance);

        public static async Task<Harness> Create()
        {
            var harness = new Harness();
            await harness.Emitter.ConnectAsync(RestrictedConnection, Restricted());
            await harness.Emitter.ConnectAsync(UnrestrictedConnection, Unrestricted());
            return harness;
        }
    }
}
