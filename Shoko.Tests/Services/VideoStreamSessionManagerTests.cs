using System;
using System.Linq;
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
            new Mock<IApplicationPaths>().Object,
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
