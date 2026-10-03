using System;
using System.IO;
using ImageMagick;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Plugin;
using Xunit;

using TmdbPlugin = Shoko.Plugin.Tmdb.Plugin;
using WebAOMPlugin = Shoko.Plugin.WebAOM.Plugin;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Checks that each bundled plugin ships the thumbnail and icon it names,
///   in a format the server reads.
/// </summary>
public class BundledPluginImageTests
{
    #region Helpers

    private static string ReadMimeType(Type pluginType, string? resourceName)
    {
        Assert.NotNull(resourceName);
        using var stream = pluginType.Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        return PackageImageLoader.GetMimeFromFormat(new MagickImageInfo(memory.ToArray())) ?? string.Empty;
    }

    #endregion

    #region Images

    [Theory]
    [InlineData(typeof(TmdbPlugin))]
    [InlineData(typeof(WebAOMPlugin))]
    public void ThePluginShipsItsThumbnailAndIcon(Type pluginType)
    {
        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;

        Assert.Equal("image/svg+xml", ReadMimeType(pluginType, plugin.EmbeddedThumbnailResourceName));
        Assert.Equal("image/svg+xml", ReadMimeType(pluginType, plugin.EmbeddedIconResourceName));
    }

    #endregion
}
