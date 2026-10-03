using System;
using System.IO;
using ImageMagick;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Covers how a plugin's images are found beside it or extracted from the
/// bytes it embedded, in a plugin directory and beside a loose dll.
/// </summary>
public sealed class PackageImageLoaderTests : IDisposable
{
    #region Fixture

    private readonly string _root = Directory.CreateTempSubdirectory("shoko-image-loader-").FullName;

    private readonly IApplicationPaths _paths;

    public PackageImageLoaderTests()
        => _paths = Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == _root && paths.ApplicationPath == Path.Combine(_root, "app"));

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    private static byte[] Png()
    {
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        return image.ToByteArray(MagickFormat.Png);
    }

    private string PluginDirectory()
        => Directory.CreateDirectory(Path.Combine(_root, "Example")).FullName;

    #endregion

    #region Plugin Images

    [Fact]
    public void AnEmbeddedImageIsWrittenIntoThePluginDirectory()
    {
        var directory = PluginDirectory();

        var image = PackageImageLoader.Load(directory, Path.Combine(directory, "Example.dll"), Png(), "icon", _paths);

        Assert.NotNull(image);
        Assert.Equal(("image/png", 2, 2), (image.MimeType, image.Width, image.Height));
        Assert.Equal("%PluginsPath%/Example/icon.png", image.FilePath.Replace('\\', '/'));
        Assert.True(File.Exists(Path.Combine(directory, "icon.png")));
    }

    [Fact]
    public void AnEmbeddedImageOfALooseDllIsNamedAfterIt()
    {
        var image = PackageImageLoader.Load(null, Path.Combine(_root, "Example.dll"), Png(), "thumbnail", _paths);

        Assert.Equal("%PluginsPath%/Example.thumbnail.png", image?.FilePath.Replace('\\', '/'));
    }

    [Fact]
    public void AFileBesideThePluginWinsOverTheEmbeddedBytes()
    {
        var directory = PluginDirectory();
        File.WriteAllBytes(Path.Combine(directory, "icon.png"), Png());

        var image = PackageImageLoader.Load(directory, Path.Combine(directory, "Example.dll"), [0, 1, 2, 3, 4, 5, 6, 7, 8, 9], "icon", _paths);

        Assert.Equal("%PluginsPath%/Example/icon.png", image?.FilePath.Replace('\\', '/'));
    }

    [Fact]
    public void NothingEmbeddedAndNothingBesideIsNoImage()
        => Assert.Null(PackageImageLoader.Load(PluginDirectory(), Path.Combine(_root, "Example", "Example.dll"), null, "icon", _paths));

    #endregion

    #region Source Icons

    [Fact]
    public void ASourceIconIsNamedAfterItsSourceInThePluginDirectory()
    {
        var directory = PluginDirectory();
        var kind = MetadataProviderManager.SourceIconKind(TestSources.AniList);

        var icon = PackageImageLoader.Load(directory, Path.Combine(directory, "Example.dll"), Png(), kind, _paths);

        Assert.Equal("%PluginsPath%/Example/anilist-icon.png", icon?.FilePath.Replace('\\', '/'));
        // The plugin's own icon is not taken for it.
        Assert.Null(PackageImageLoader.Load(directory, Path.Combine(directory, "Example.dll"), null, "icon", _paths));
    }

    [Fact]
    public void ASourceIconOfALooseDllIsNamedAfterTheDllAndItsSource()
    {
        var kind = MetadataProviderManager.SourceIconKind(TestSources.AniList);

        var icon = PackageImageLoader.Load(null, Path.Combine(_root, "Example.dll"), Png(), kind, _paths);

        Assert.Equal("%PluginsPath%/Example.anilist-icon.png", icon?.FilePath.Replace('\\', '/'));
    }

    #endregion

    #region Image Contributor Icons

    [Fact]
    public void AContributorIconIsNamedAfterItsSource_ApartFromTheSourceIcon()
    {
        var directory = PluginDirectory();
        var dll = Path.Combine(directory, "Example.dll");

        var icon = PackageImageLoader.Load(directory, dll, Png(), MetadataImageContributorManager.IconKind(TestSources.AniList), _paths);

        Assert.Equal("%PluginsPath%/Example/anilist.images-icon.png", icon?.FilePath.Replace('\\', '/'));
        // Neither the source's icon nor the plugin's is taken for it.
        Assert.Null(PackageImageLoader.Load(directory, dll, null, MetadataProviderManager.SourceIconKind(TestSources.AniList), _paths));
        Assert.Null(PackageImageLoader.Load(directory, dll, null, "icon", _paths));
    }

    [Fact]
    public void AContributorIconOfALooseDllIsNamedAfterTheDllAndItsSource()
    {
        var icon = PackageImageLoader.Load(null, Path.Combine(_root, "Example.dll"), Png(), MetadataImageContributorManager.IconKind(TestSources.AniList), _paths);

        Assert.Equal("%PluginsPath%/Example.anilist.images-icon.png", icon?.FilePath.Replace('\\', '/'));
    }

    #endregion
}
