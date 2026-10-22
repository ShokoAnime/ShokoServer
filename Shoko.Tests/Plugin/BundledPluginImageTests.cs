using System;
using System.IO;
using ImageMagick;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Plugin;
using Xunit;

using LegacyPlexPlugin = Shoko.Plugin.LegacyPlex.Plugin;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Checks that the core and each bundled plugin ship their thumbnail and
///   icon, in a format the server reads.
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
    [InlineData(typeof(LegacyPlexPlugin))]
    public void ThePluginShipsItsThumbnailAndIcon(Type pluginType)
    {
        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;

        Assert.Equal("image/svg+xml", ReadMimeType(pluginType, plugin.EmbeddedThumbnailResourceName));
        Assert.Equal("image/svg+xml", ReadMimeType(pluginType, plugin.EmbeddedIconResourceName));
    }

    [Theory]
    [InlineData("thumbnail")]
    [InlineData("icon")]
    public void TheCoreShipsItsImagesBesideItsDll(string kind)
    {
        var dll = typeof(CorePlugin).Assembly.Location;
        var fileName = Path.ChangeExtension(dll, $".{kind}.svg");

        Assert.True(File.Exists(fileName), $"{Path.GetFileName(fileName)} is missing beside {Path.GetFileName(dll)}.");
        Assert.Equal("image/svg+xml", PackageImageLoader.GetMimeFromFormat(new MagickImageInfo(fileName)));
    }

    #endregion
}
