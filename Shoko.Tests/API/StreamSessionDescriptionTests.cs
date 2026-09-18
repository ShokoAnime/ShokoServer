using System;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.API.v3.Models.Streaming;
using Xunit;

namespace Shoko.Tests.API;

public class StreamSessionDescriptionTests
{
    private const string Root = "/api/v3/File/5/Stream/Hls/3f2504e0-4f89-11d3-9a0c-0305e82c3301/";

    [Theory]
    [InlineData("subtitles/2.ass", "", Root + "subtitles/2.ass")]
    [InlineData("subtitles/2.ass", "?apikey=abc", Root + "subtitles/2.ass?apikey=abc")]
    [InlineData("/fonts/0.ttf", "?apikey=abc", Root + "fonts/0.ttf?apikey=abc")]
    [InlineData("preview.mp4?height=240", "?apikey=abc", Root + "preview.mp4?height=240&apikey=abc")]
    public void ResolveUrl_CarriesTheQueryString(string path, string query, string expected)
        => Assert.Equal(expected, StreamSessionDescription.ResolveUrl(Root, path, query));

    [Fact]
    public void Constructor_ResolvesEveryResourcePath()
    {
        var description = new StreamDescription
        {
            Subtitles = [new() { Ordinal = 0, Format = "vobsub", Path = "subtitles/0.idx", CompanionPaths = ["subtitles/0.sub"] }],
            Attachments = [new() { Index = 0, FileName = "font.ttf", ContentType = "font/ttf", Path = "attachments/0" }],
            Extras = [new() { Kind = "seek-preview", ContentType = "video/mp4", Path = "preview.mp4" }],
        };

        var session = new StreamSessionDescription(Guid.Empty, "Plugin:Transform", StreamDeliveryMode.Hls, Root + "master.m3u8", description,
            path => StreamSessionDescription.ResolveUrl(Root, path, "?apikey=abc"));

        Assert.Equal(Root + "subtitles/0.idx?apikey=abc", session.Subtitles[0].Url);
        Assert.Equal([Root + "subtitles/0.sub?apikey=abc"], session.Subtitles[0].CompanionUrls);
        Assert.Equal(Root + "attachments/0?apikey=abc", session.Attachments[0].Url);
        Assert.Equal(Root + "preview.mp4?apikey=abc", session.Extras[0].Url);
    }
}
