using System.Text;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Services;

public class ImageFormatTests
{
    [Fact]
    public void AnSvgIsDetectedAndItsSizeRead()
    {
        var data = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"16\"><rect width=\"24\" height=\"16\"/></svg>");

        Assert.Equal("image/svg+xml", ImageManager.GetImageMimeType(data));
        Assert.True(ImageManager.TryReadImage(data, out var width, out var height));
        Assert.Equal((24, 16), (width, height));
    }
}
