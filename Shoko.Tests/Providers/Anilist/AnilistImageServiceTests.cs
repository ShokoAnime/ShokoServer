using Shoko.Server.Providers.Anilist;
using Xunit;

namespace Shoko.Tests.Providers.Anilist;

public class AnilistImageServiceTests
{
    [Theory]
    [InlineData("https://s4.anilist.co/file/anilistcdn/media/anime/cover/large/bx21-YCDoj1EkAxFn.jpg", "media/anime/cover/large/bx21-YCDoj1EkAxFn.jpg")]
    [InlineData("https://s4.anilist.co/file/anilistcdn/media/anime/banner/21-wf37VakJmZqs.jpg", "media/anime/banner/21-wf37VakJmZqs.jpg")]
    [InlineData("https://s4.anilist.co/file/anilistcdn/character/large/b40-Bn6Bmf3LLIf3.png", "character/large/b40-Bn6Bmf3LLIf3.png")]
    [InlineData("https://s4.anilist.co/file/anilistcdn/staff/large/n95269-3Yw3ycwWjc0j.jpg", "staff/large/n95269-3Yw3ycwWjc0j.jpg")]
    public void ToResourceID_StripsTheCdnBase(string url, string expected)
        => Assert.Equal(expected, AnilistImageService.ToResourceID(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ToResourceID_EmptyUrl_ReturnsNull(string? url)
        => Assert.Null(AnilistImageService.ToResourceID(url));

    [Fact]
    public void ToResourceID_UnknownHost_KeepsTheFullUrl()
        => Assert.Equal("https://example.org/image.png", AnilistImageService.ToResourceID("https://example.org/image.png"));

    [Fact]
    public void ToResourceID_RecordsTheObservedCdnBase()
    {
        AnilistImageService.ToResourceID("https://s9.anilist.co/file/anilistcdn/media/anime/cover/large/bx1-x.jpg");
        Assert.Equal("https://s9.anilist.co/file/anilistcdn/", AnilistImageService.ImageServerUrl);

        // Put it back so other tests see the default.
        AnilistImageService.ToResourceID(AnilistImageService.DefaultImageServerUrl + "media/anime/cover/large/bx1-x.jpg");
        Assert.Equal(AnilistImageService.DefaultImageServerUrl, AnilistImageService.ImageServerUrl);
    }
}
