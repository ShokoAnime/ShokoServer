using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Xunit;

using InstallReason = Shoko.Server.Plugin.PluginPackageManager.InstallReason;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Plugins installed as dependencies: when an install marks them, when
///   they are enabled on demand, when they are orphans, and how they hold
///   upgrades back.
/// </summary>
public class PluginDependencyMarkerTests
{
    private static readonly Guid _app = Guid.Parse("00000000-0000-4000-8000-00000000000a");

    private static readonly Guid _library = Guid.Parse("00000000-0000-4000-8000-00000000000b");

    private static readonly Guid _core = Guid.Parse("00000000-0000-4000-8000-00000000000c");

    #region Helpers

    private static PluginDependency Requires(Guid id, string range = ">=1.0.0", bool optional = false)
        => new() { PluginID = id, VersionRange = range, IsOptional = optional };

    private static PluginManager.DemandNode Node(Guid id, bool isDependency, bool isEnabled = false, string version = "1.0.0", params PluginDependency[] dependencies)
        => new(id, id.ToString(), isDependency, isEnabled, false, Version.Parse(version), dependencies);

    private static LocalPluginInfo Local(Guid id, bool isDependency, params PluginDependency[] dependencies)
        => new()
        {
            ID = id,
            Name = id.ToString(),
            Description = string.Empty,
            Version = new()
            {
                Version = new(1, 0, 0),
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
            InstalledAt = DateTime.UnixEpoch,
            IsEnabled = true,
            IsActive = false,
            IsDependency = isDependency,
            CanUninstall = true,
            Plugin = null,
            PluginType = null,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = [$"{id}.dll"],
            Types = [],
            Dependencies = dependencies,
        };

    #endregion

    #region Install Marks

    [Theory]
    [InlineData((byte)1, new bool[0], false)]
    [InlineData((byte)1, new[] { true }, false)]
    [InlineData((byte)2, new bool[0], true)]
    [InlineData((byte)2, new[] { true, true }, true)]
    [InlineData((byte)2, new[] { true, false }, false)]
    [InlineData((byte)3, new[] { true }, true)]
    [InlineData((byte)3, new[] { true, false }, false)]
    public void Install_MarksOnlyWhenEveryOtherVersionIsMarked(byte reason, bool[] otherVersionsMarked, bool expected)
        => Assert.Equal(expected, PluginPackageManager.ShouldMarkAsDependency((InstallReason)reason, otherVersionsMarked));

    #endregion

    #region Demand

    [Fact]
    public void Dependency_IsDemanded_ThroughAChain()
    {
        var (onDemand, demanded) = PluginManager.FindDemand(
            [
                Node(_app, isDependency: false, isEnabled: true, dependencies: Requires(_library)),
                Node(_library, isDependency: true, dependencies: Requires(_core, optional: true)),
                Node(_core, isDependency: true),
            ],
            new Dictionary<string, bool> { [_app.ToString()] = true }
        );

        Assert.Equal([_library, _core], onDemand.Order());
        Assert.Equal([_library, _core], demanded.Order());
    }

    [Fact]
    public void Dependency_OfADisabledPlugin_IsNotDemanded()
    {
        var (onDemand, demanded) = PluginManager.FindDemand(
            [Node(_app, isDependency: false, isEnabled: false, dependencies: Requires(_library)), Node(_library, isDependency: true)],
            new Dictionary<string, bool> { [_app.ToString()] = false }
        );

        Assert.Equal([_library], onDemand);
        Assert.Empty(demanded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnabledState_OrUnmarkedVersion_TakesThePluginOffDemand(bool hasState)
    {
        var (onDemand, _) = PluginManager.FindDemand(
            hasState ? [Node(_library, isDependency: true)] : [Node(_library, isDependency: true), Node(_library, isDependency: false, version: "2.0.0")],
            hasState ? new Dictionary<string, bool> { [_library.ToString()] = false } : []
        );

        Assert.Empty(onDemand);
    }

    #endregion

    #region Orphans

    [Fact]
    public void Orphans_AreTheDependenciesNothingInstalledOnPurposeNeeds()
    {
        var orphans = PluginManager.FindOrphans([
            Local(_app, isDependency: false, Requires(_library)),
            Local(_library, isDependency: true, Requires(_core, optional: true)),
            Local(_core, isDependency: true),
            Local(Guid.Parse("00000000-0000-4000-8000-0000000000dd"), isDependency: true, Requires(_library)),
        ]);

        Assert.Equal([Guid.Parse("00000000-0000-4000-8000-0000000000dd")], orphans);
    }

    #endregion

    #region Upgrades

    [Fact]
    public void Upgrade_OutsideADependentsRange_IsHeldBack()
    {
        var manifest = new PackageManifestInfo
        {
            PackageID = _library,
            Name = "Library",
            Overview = string.Empty,
            Authors = string.Empty,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            LastFetchedAt = DateTime.UnixEpoch,
            Releases = new[] { "2.0.0", "1.5.0" }
                .Select(version => new PackageReleaseInfo
                {
                    RepositoryID = null,
                    Version = Version.Parse(version),
                    Tag = null,
                    SourceRevision = null,
                    ReleasedAt = DateTime.UnixEpoch,
                    Channel = ReleaseChannel.Stable,
                    ReleaseNotes = null,
                    Archives = [new() { RuntimeIdentifier = IPluginManager.AnyRuntimeIdentifier, AbstractionVersion = new(6, 0, 0), ArchiveUrl = "https://repo.test/a.zip", ArchiveChecksum = string.Empty }],
                })
                .ToList(),
        };

        var upgrades = PluginPackageManager.SelectUpgrades(
            [
                new(_library, "Library", new(1, 0, 0), true, false),
                new(_app, "App", new(1, 0, 0), true, false, Dependencies: [Requires(_library, "^1.0.0")]),
            ],
            [manifest],
            [],
            (_, runtimeIdentifier) => runtimeIdentifier is IPluginManager.AnyRuntimeIdentifier,
            "linux-x64"
        );

        Assert.Equal(new Version(1, 5, 0), Assert.Single(upgrades).Release.Version);
    }

    #endregion
}
