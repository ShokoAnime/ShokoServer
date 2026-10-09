using System;
using System.IO;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Unit tests for which inactive plugin versions
/// <see cref="PluginPackageManager.SelectInactivePluginVersions"/> picks for removal,
/// and for the <c>.keep</c> file that protects a version from it.
/// </summary>
public class InactivePluginVersionPurgeTests : IDisposable
{
    private static readonly Guid _id = new("44444444-4444-4444-4444-444444444444");

    private static readonly DateTime _start = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan _retention = TimeSpan.FromDays(30);

    private readonly string _root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"shoko-inactive-plugin-purge-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// Makes a plugin version installed on <paramref name="installedOnDay"/>, in a
    /// directory of its own unless <paramref name="singleDll"/> places it as a
    /// lone DLL in the root.
    /// </summary>
    private LocalPluginInfo MakePlugin(
        string version,
        int installedOnDay = 0,
        bool isActive = false,
        bool isPinned = false,
        bool canUninstall = true,
        bool singleDll = false,
        Guid? id = null
    )
    {
        var directory = singleDll ? null : Directory.CreateDirectory(Path.Join(_root, Guid.NewGuid().ToString("N"))).FullName;
        var dllPath = Path.Join(directory ?? _root, $"Plugin{version}.dll");
        File.WriteAllBytes(dllPath, []);

        return new()
        {
            ID = id ?? _id,
            Name = "Plugin",
            Description = string.Empty,
            Version = new()
            {
                Version = Version.Parse(version),
                RuntimeIdentifier = "any",
                AbstractionVersion = new(6, 0, 0),
                SourceRevision = null,
                ReleaseTag = null,
                Channel = ReleaseChannel.Stable,
                ReleasedAt = DateTime.UnixEpoch,
            },
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            InstalledAt = _start.AddDays(installedOnDay),
            IsEnabled = isActive,
            IsPinned = isPinned,
            IsActive = isActive,
            CanLoad = true,
            CanUninstall = canUninstall,
            Plugin = null,
            PluginType = null,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = directory,
            DLLs = [dllPath],
            Types = [],
            Dependencies = [],
        };
    }

    private static LocalPluginInfo[] Select(int onDay, params LocalPluginInfo[] pluginInfos)
        => [.. PluginPackageManager.SelectInactivePluginVersions(pluginInfos, _retention, _start.AddDays(onDay))];

    [Fact]
    public void OlderVersion_IsSelectedOnceRetentionPassedSinceSuperseded()
    {
        var old = MakePlugin("1.0.0");
        var active = MakePlugin("2.0.0", installedOnDay: 10, isActive: true);

        Assert.Empty(Select(39, old, active));
        Assert.Equal([old], Select(41, old, active));
    }

    [Fact]
    public void OlderVersion_IsSupersededByTheFirstNewerInstall()
    {
        var old = MakePlugin("1.0.0");
        var skipped = MakePlugin("2.0.0", installedOnDay: 10);
        var active = MakePlugin("3.0.0", installedOnDay: 35, isActive: true);

        Assert.Equal([old], Select(41, old, skipped, active));
    }

    [Fact]
    public void PendingUpdate_IsKept()
    {
        var pending = MakePlugin("3.0.0");
        var active = MakePlugin("2.0.0", isActive: true);

        Assert.Empty(Select(1000, pending, active));
    }

    [Fact]
    public void NewerVersionWithPinnedActive_IsSelectedOnceRetentionPassedSinceInstall()
    {
        var newer = MakePlugin("3.0.0", installedOnDay: 10);
        var pinned = MakePlugin("2.0.0", isActive: true, isPinned: true);

        Assert.Empty(Select(39, newer, pinned));
        Assert.Equal([newer], Select(41, newer, pinned));
    }

    [Fact]
    public void PinnedUninstallableAndPluginsWithoutActiveVersion_AreKept()
    {
        var pinned = MakePlugin("1.0.0", isPinned: true);
        var system = MakePlugin("1.5.0", canUninstall: false);
        var active = MakePlugin("2.0.0", isActive: true);

        var otherID = new Guid("55555555-5555-5555-5555-555555555555");
        var inactiveOld = MakePlugin("1.0.0", id: otherID);
        var inactiveNew = MakePlugin("2.0.0", id: otherID);

        Assert.Empty(Select(1000, pinned, system, active, inactiveOld, inactiveNew));
    }

    [Fact]
    public void ZeroRetention_SelectsAtOnce()
    {
        var old = MakePlugin("1.0.0");
        var active = MakePlugin("2.0.0", installedOnDay: 10, isActive: true);

        Assert.Equal([old], PluginPackageManager.SelectInactivePluginVersions([old, active], TimeSpan.Zero, _start.AddDays(10)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeepAndUnkeep_WriteAndDeleteTheKeepFile(bool singleDll)
    {
        var manager = TestPluginManager.Create();
        var old = MakePlugin("1.0.0", singleDll: singleDll);
        var active = MakePlugin("2.0.0", isActive: true);
        var keepFile = singleDll ? Path.Join(_root, "Plugin1.0.0.keep") : Path.Join(old.ContainingDirectory, ".keep");

        manager.KeepPlugin(old);
        Assert.True(old.IsKept);
        Assert.True(File.Exists(keepFile));
        Assert.Equal(keepFile, PluginManager.GetKeepFile(old.ContainingDirectory, old.DLLs[0]));
        Assert.Empty(Select(1000, old, active));

        manager.UnkeepPlugin(old);
        Assert.False(old.IsKept);
        Assert.False(File.Exists(keepFile));
        Assert.Equal([old], Select(1000, old, active));
    }

    [Fact]
    public void Keep_OnVersionThatCannotBeUninstalled_WritesNothing()
    {
        var manager = TestPluginManager.Create();
        var system = MakePlugin("1.0.0", canUninstall: false);

        manager.KeepPlugin(system);

        Assert.False(system.IsKept);
        Assert.False(File.Exists(Path.Join(system.ContainingDirectory, ".keep")));
    }
}
