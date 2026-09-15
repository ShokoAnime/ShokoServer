using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

public class VideoStreamSessionManagerTests
{
    [Fact]
    public void BuildManifest_WritesOneSegmentPerDurationWithTheRemainderLast()
    {
        var manifest = CreateManager().BuildManifest(CreateVideo(TimeSpan.FromSeconds(15)), CreateRendition(TimeSpan.FromSeconds(6)), string.Empty);
        var lines = manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(["#EXTINF:6.000,", "#EXTINF:6.000,", "#EXTINF:3.000,"], lines.Where(line => line.StartsWith("#EXTINF")));
        Assert.Contains("#EXT-X-TARGETDURATION:6", lines);
        Assert.Equal("#EXT-X-ENDLIST", lines[^1]);
    }

    [Fact]
    public void BuildManifest_CarriesTheQueryStringOnEveryUri()
    {
        var manifest = CreateManager().BuildManifest(CreateVideo(TimeSpan.FromSeconds(12)), CreateRendition(TimeSpan.FromSeconds(6)), "?apikey=abc");
        var lines = manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Contains("#EXT-X-MAP:URI=\"init.mp4?apikey=abc\"", lines);
        Assert.Equal(["segment-0.m4s?apikey=abc", "segment-1.m4s?apikey=abc"], lines.Where(line => !line.StartsWith('#')));
    }

    [Fact]
    public async Task EvictExpiredSessions_KeepsASessionWhileAResponseIsStreaming()
    {
        var manager = CreateManager();
        var sessionId = manager.CreateSession(CreateVideo(TimeSpan.FromMinutes(24)), CreateRendition(TimeSpan.FromSeconds(6)));
        var stream = manager.TryGetSession(sessionId)!.Track(new MemoryStream([1, 2, 3]), new DefaultHttpContext().Response);

        manager.EvictExpiredSessions(TimeSpan.FromMinutes(-1));
        Assert.NotNull(manager.TryGetSession(sessionId));

        await stream.DisposeAsync();
        manager.EvictExpiredSessions(TimeSpan.FromMinutes(-1));
        Assert.Null(manager.TryGetSession(sessionId));
    }

    [Fact]
    public void Track_ReleasesOnlyOnceWhenDisposedTwice()
    {
        var manager = CreateManager();
        var session = manager.TryGetSession(manager.CreateSession(CreateVideo(TimeSpan.FromMinutes(24)), CreateRendition(TimeSpan.FromSeconds(6))))!;
        var first = session.Track(new MemoryStream(), new DefaultHttpContext().Response);
        using var second = session.Track(new MemoryStream(), new DefaultHttpContext().Response);

        first.Dispose();
        first.Dispose();

        Assert.True(session.IsInUse);
    }

    [Fact]
    public async Task GetOrRestoreSessionAsync_RebuildsAnEvictedSessionOnceUnderTheSameId()
    {
        var manager = CreateManager();
        var video = CreateVideo(TimeSpan.FromMinutes(24));
        var sessionId = manager.CreateSession(video, CreateRendition(TimeSpan.FromSeconds(6)), source: CreateSource());
        var originalCacheDir = manager.TryGetSession(sessionId)!.CacheDir;
        manager.EvictExpiredSessions(TimeSpan.FromMinutes(-1));
        Assert.Null(manager.TryGetSession(sessionId));

        var builds = 0;
        var release = new TaskCompletionSource();
        async Task<(IVideo, IStreamRendition)?> Factory(StreamSessionSource source, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref builds);
            await release.Task;
            return (video, CreateRendition(TimeSpan.FromSeconds(6)));
        }

        var first = manager.GetOrRestoreSessionAsync(sessionId, Factory, CancellationToken.None);
        var second = manager.GetOrRestoreSessionAsync(sessionId, Factory, CancellationToken.None);
        release.SetResult();
        var sessions = await Task.WhenAll(first, second);

        Assert.Equal(1, builds);
        Assert.Same(sessions[0], sessions[1]);
        Assert.Same(sessions[0], manager.TryGetSession(sessionId));
        Assert.Equal("Plugin:Transform", sessions[0]!.TransformID);
        Assert.NotEqual(originalCacheDir, sessions[0]!.CacheDir);
    }

    [Fact]
    public async Task GetOrRestoreSessionAsync_DoesNotRebuildASessionWithoutASource()
    {
        var manager = CreateManager();
        var sessionId = manager.CreateSession(CreateVideo(TimeSpan.FromMinutes(24)), CreateRendition(TimeSpan.FromSeconds(6)));
        manager.EvictExpiredSessions(TimeSpan.FromMinutes(-1));

        Assert.Null(await manager.GetOrRestoreSessionAsync(sessionId, FailingFactory, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrRestoreSessionAsync_DoesNotRebuildASessionPastTheResumeWindow()
    {
        var manager = CreateManager();
        var sessionId = manager.CreateSession(CreateVideo(TimeSpan.FromMinutes(24)), CreateRendition(TimeSpan.FromSeconds(6)), source: CreateSource());
        manager.EvictExpiredSessions(TimeSpan.FromMinutes(-1));
        manager.EvictExpiredSessions(TimeSpan.FromMinutes(-1), resumeWindow: TimeSpan.FromMinutes(-1));

        Assert.Null(await manager.GetOrRestoreSessionAsync(sessionId, FailingFactory, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrRestoreSessionAsync_DoesNotRebuildAnUnknownSession()
        => Assert.Null(await CreateManager().GetOrRestoreSessionAsync(Guid.NewGuid(), FailingFactory, CancellationToken.None));

    private static Task<(IVideo, IStreamRendition)?> FailingFactory(StreamSessionSource source, CancellationToken cancellationToken)
        => throw new InvalidOperationException("The session should not have been rebuilt.");

    private static StreamSessionSource CreateSource()
        => new(1, "Plugin:Transform", new QueryCollection());

    private static VideoStreamSessionManager CreateManager()
    {
        var configurationService = new Mock<IConfigurationService>();
        configurationService
            .Setup(s => s.GetConfigurationInfo<VideoStreamPipelineSettings>())
            .Returns((ConfigurationInfo)null!);
        configurationService
            .Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
            .Returns(new VideoStreamPipelineSettings());

        return new VideoStreamSessionManager(
            NullLogger<VideoStreamSessionManager>.Instance,
            Mock.Of<IApplicationPaths>(paths => paths.StreamCachePath == Path.Combine(Path.GetTempPath(), "shoko-stream-session-tests")),
            new ConfigurationProvider<VideoStreamPipelineSettings>(configurationService.Object)
        );
    }

    private static IVideo CreateVideo(TimeSpan duration)
    {
        var mediaInfo = new Mock<IMediaInfo>();
        mediaInfo.Setup(m => m.Duration).Returns(duration);
        var video = new Mock<IVideo>();
        video.Setup(v => v.MediaInfo).Returns(mediaInfo.Object);
        return video.Object;
    }

    private static IHlsStreamRendition CreateRendition(TimeSpan segmentDuration)
    {
        var rendition = new Mock<IHlsStreamRendition>();
        rendition.Setup(r => r.SegmentDuration).Returns(segmentDuration);
        return rendition.Object;
    }
}
