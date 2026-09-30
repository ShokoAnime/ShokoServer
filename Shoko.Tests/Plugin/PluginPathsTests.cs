using System;
using System.IO;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Covers <see cref="PluginPaths{TPlugin}"/>: which folders are the plugin's own, when they are
/// created, where it is installed and the files resolved inside its folders.
/// </summary>
public sealed class PluginPathsTests : IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), $"shoko-plugin-paths-{Guid.NewGuid():N}");

    private readonly Mock<IApplicationPaths> _applicationPaths = new();

    public PluginPathsTests()
    {
        _applicationPaths.Setup(paths => paths.DataPath).Returns(_root);
        _applicationPaths.Setup(paths => paths.PluginsPath).Returns(Path.Join(_root, "plugins"));
        _applicationPaths.Setup(paths => paths.ConfigurationsPath).Returns(Path.Join(_root, "configuration"));
        _applicationPaths.Setup(paths => paths.DatabasePath).Returns(Path.Join(_root, "data"));
        _applicationPaths.Setup(paths => paths.CachePath).Returns(Path.Join(_root, "cache"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    #region Folders

    [Fact]
    public void TheOwnFoldersAreNamedByThePluginIDAndCreatedWhenFirstRead()
    {
        var id = Guid.NewGuid();
        var paths = new PluginPaths<FirstPlugin>(_applicationPaths.Object, (id, Path.Join(_root, "plugins", "First")));

        Assert.False(Directory.Exists(Path.Join(_root, "data", id.ToString())));

        Assert.Equal(Path.Join(_root, "configuration", id.ToString()), paths.ConfigurationsPath);
        Assert.Equal(Path.Join(_root, "data", id.ToString()), paths.DatabasePath);
        Assert.Equal(Path.Join(_root, "cache", id.ToString()), paths.CachePath);
        Assert.True(Directory.Exists(paths.ConfigurationsPath));
        Assert.True(Directory.Exists(paths.DatabasePath));
        Assert.True(Directory.Exists(paths.CachePath));
        Assert.Equal(id, paths.PluginID);
    }

    [Fact]
    public void TheInstallFolderIsThePluginsFolderOrTheFolderOfItsDll()
    {
        var inFolder = PluginTestDoubles.InstalledPluginInfoInFolder(typeof(FirstPlugin), Guid.NewGuid(), Path.Join(_root, "plugins", "First"), Path.Join(_root, "plugins", "First", "First.dll"));
        var singleFile = PluginTestDoubles.InstalledPluginInfo(typeof(SecondPlugin), Guid.NewGuid(), Path.Join(_root, "plugins", "Second.dll"));

        Assert.Equal(Path.Join(_root, "plugins", "First"), new PluginPaths<FirstPlugin>(_applicationPaths.Object, PluginManagerFor(inFolder, singleFile)).InstallPath);
        Assert.Equal(Path.Join(_root, "plugins"), new PluginPaths<SecondPlugin>(_applicationPaths.Object, PluginManagerFor(inFolder, singleFile)).InstallPath);
    }

    [Fact]
    public void APluginThatIsNotLoadedHasNoPaths()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new PluginPaths<FirstPlugin>(_applicationPaths.Object, Mock.Of<IPluginManager>()));
        Assert.Contains(typeof(FirstPlugin).FullName!, ex.Message);
    }

    #endregion

    #region Files

    [Fact]
    public void FilesResolveInsideTheOwnFoldersWithTheirFolderCreated()
    {
        var paths = new PluginPaths<FirstPlugin>(_applicationPaths.Object, (Guid.NewGuid(), _root));

        var database = paths.GetDatabaseFile(Path.Join("nested", "notes.db3"));
        var cache = paths.GetCacheFile("thumbnail.png");

        Assert.Equal(Path.Join(paths.DatabasePath, "nested", "notes.db3"), database);
        Assert.Equal(Path.Join(paths.CachePath, "thumbnail.png"), cache);
        Assert.True(Directory.Exists(Path.Join(paths.DatabasePath, "nested")));
        Assert.False(File.Exists(database));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape.db3")]
    [InlineData("nested/../../escape.db3")]
    public void FilesOutsideTheOwnFoldersAreRefused(string relativePath)
    {
        var paths = new PluginPaths<FirstPlugin>(_applicationPaths.Object, (Guid.NewGuid(), _root));

        Assert.Throws<ArgumentException>(() => paths.GetDatabaseFile(relativePath));
        Assert.Throws<ArgumentException>(() => paths.GetCacheFile(relativePath));
        Assert.Throws<ArgumentException>(() => paths.GetDatabaseFile(Path.Join(_root, "rooted.db3")));
    }

    #endregion

    #region Helpers

    private static IPluginManager PluginManagerFor(Abstractions.Plugin.Models.LocalPluginInfo first, Abstractions.Plugin.Models.LocalPluginInfo second)
    {
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfo<FirstPlugin>()).Returns(first);
        pluginManager.Setup(manager => manager.GetPluginInfo<SecondPlugin>()).Returns(second);
        return pluginManager.Object;
    }

    public sealed class FirstPlugin : IPlugin
    {
        public Guid ID { get; } = Guid.NewGuid();

        public string Name => "First";
    }

    public sealed class SecondPlugin : IPlugin
    {
        public Guid ID { get; } = Guid.NewGuid();

        public string Name => "Second";
    }

    #endregion
}
