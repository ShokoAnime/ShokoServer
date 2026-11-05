using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Shoko.Abstractions.Plugin.Exceptions;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   An archive is unpacked file by file, held to the unpacked size its
///   manifest gives and kept inside the plugin's folder, and an installed
///   plugin's folder is measured on disk.
/// </summary>
public sealed class PackageArchiveExtractionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("package-archive-extraction-").FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    private string Zip(params (string Name, int Length)[] entries)
    {
        var path = Path.Join(_root, $"{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, length) in entries)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(new byte[length]);
        }

        return path;
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(300L)]
    public void AnArchiveOfTheRightSizeIsUnpacked(long expectedUnpackedSize)
    {
        var extractPath = Path.Join(_root, "plugin");

        PluginPackageManager.ExtractArchive(Zip(("Plugin.dll", 100), ("lib/Other.dll", 200)), extractPath, expectedUnpackedSize, CancellationToken.None);

        Assert.Equal(300, PluginManager.MeasureInstalledSize(extractPath));
    }

    [Theory]
    [InlineData(299L, 300L)]
    [InlineData(301L, 300L)]
    public void AnArchiveUnpackingToAnotherSizeIsRefused(long expectedUnpackedSize, long actualSize)
    {
        var refused = Assert.Throws<InvalidArchiveSizeException>(
            () => PluginPackageManager.ExtractArchive(Zip(("Plugin.dll", 100), ("lib/Other.dll", 200)), Path.Join(_root, "plugin"), expectedUnpackedSize, CancellationToken.None)
        );

        Assert.True(refused.IsUnpackedSize);
        Assert.Equal(actualSize, refused.ActualSize);
    }

    [Fact]
    public void AnEntryOutsideThePluginFolderIsRefused()
    {
        Assert.Throws<IOException>(() => PluginPackageManager.ExtractArchive(Zip(("../escaped.dll", 10)), Path.Join(_root, "plugin"), 0, CancellationToken.None));

        Assert.False(File.Exists(Path.Join(_root, "escaped.dll")));
    }

    [Fact]
    public void ThereIsNothingToMeasureWithoutAFolder()
        => Assert.Equal(0, PluginManager.MeasureInstalledSize(null));

    [Fact]
    public void ALoneDllIsMeasuredWithTheFilesNamedAfterIt()
    {
        foreach (var (name, length) in new[] { ("Lone.dll", 100), ("Lone.pdb", 20), ("Lone.deps.json", 5), ("Lone.icon.png", 7), ("Lone.thumbnail.webp", 3), ("Lone.Other.dll", 1000), ("Other.dll", 1000) })
            File.WriteAllBytes(Path.Join(_root, name), new byte[length]);

        Assert.Equal(135, PluginManager.MeasureLoneDllSize(Path.Join(_root, "Lone.dll")));
    }

    [Fact]
    public void FoldersLeftOutAtTheTopAreNotCounted()
    {
        PluginPackageManager.ExtractArchive(
            Zip(("Shoko.Server.dll", 100), ("runtimes/native.so", 50), ("plugins/Bundled/Bundled.dll", 200), ("webui/latest.zip", 400), ("lib/webui/kept.js", 25)),
            Path.Join(_root, "app"),
            0,
            CancellationToken.None
        );

        Assert.Equal(175, PluginManager.MeasureInstalledSize(Path.Join(_root, "app"), "plugins", "webui"));
    }
}
