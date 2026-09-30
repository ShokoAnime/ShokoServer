using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Unit tests for the dependency graph pass in <see cref="PluginManager"/> covering
/// load ordering, unsatisfied dependencies and cycles, and following the
/// dependencies as they are enabled and disabled while the server runs.
/// </summary>
public class PluginDependencyGraphTests
{
    private static readonly Guid _a = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid _b = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid _c = new("33333333-3333-3333-3333-333333333333");

    private static LocalPluginInfo MakePlugin(Guid id, string name, string version = "1.0.0", bool isEnabled = true, bool canLoad = true, bool isActive = false, params PluginDependency[] dependencies)
        => new()
        {
            ID = id,
            Name = name,
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
            InstalledAt = DateTime.UnixEpoch,
            IsEnabled = isEnabled,
            IsPinned = false,
            IsActive = isActive,
            CanLoad = canLoad,
            CanUninstall = true,
            Plugin = null,
            PluginType = null,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = [name + ".dll"],
            Types = [],
            Dependencies = dependencies,
        };

    private static PluginDependency Requires(Guid id, string versionRange = ">=1.0.0")
        => new() { PluginID = id, VersionRange = versionRange };

    private static PluginDependency Optionally(Guid id, string versionRange = ">=1.0.0")
        => new() { PluginID = id, VersionRange = versionRange, IsOptional = true };

    private static void Apply(List<LocalPluginInfo> plugins)
        => PluginManager.ApplyDependencyGraph(plugins, NullLogger.Instance);

    [Fact]
    public void SatisfiedDependency_LoadsAfterItsDependency()
    {
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a));
        var dependency = MakePlugin(_a, "A");
        var plugins = new List<LocalPluginInfo> { dependent, dependency };

        Apply(plugins);

        Assert.True(dependency.CanLoad);
        Assert.True(dependent.CanLoad);
        Assert.Null(dependent.CannotLoadReason);
        Assert.True(dependency.LoadOrder < dependent.LoadOrder);
        Assert.Same(dependency, plugins[dependency.LoadOrder]);
        Assert.Same(dependent, plugins[dependent.LoadOrder]);
    }

    [Fact]
    public void MissingRequiredDependency_RefusesToLoad()
    {
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a));
        var plugins = new List<LocalPluginInfo> { dependent };

        Apply(plugins);

        Assert.False(dependent.CanLoad);
        Assert.True(dependent.IsEnabled);
        Assert.Contains("not installed", dependent.CannotLoadReason);
        // Enabled but unable to load, so not waiting on a restart.
        Assert.False(dependent.RestartPending);
    }

    [Fact]
    public void DisabledRequiredDependency_RefusesToLoad()
    {
        var dependency = MakePlugin(_a, "A", isEnabled: false);
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };

        Apply(plugins);

        Assert.False(dependent.CanLoad);
        Assert.Contains("disabled", dependent.CannotLoadReason);
    }

    [Fact]
    public void OutOfRangeRequiredDependency_RefusesToLoad()
    {
        var dependency = MakePlugin(_a, "A", version: "1.5.0");
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a, "^2.0.0"));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };

        Apply(plugins);

        Assert.True(dependency.CanLoad);
        Assert.False(dependent.CanLoad);
    }

    [Fact]
    public void UnsatisfiedDependency_CascadesToTransitiveDependents()
    {
        var middle = MakePlugin(_b, "B", dependencies: Requires(_a));
        var outer = MakePlugin(_c, "C", dependencies: Requires(_b));
        var plugins = new List<LocalPluginInfo> { outer, middle };

        Apply(plugins);

        Assert.False(middle.CanLoad);
        Assert.False(outer.CanLoad);
    }

    [Fact]
    public void DependencyCycle_RefusesEveryParticipant()
    {
        var first = MakePlugin(_a, "A", dependencies: Requires(_b));
        var second = MakePlugin(_b, "B", dependencies: Requires(_a));
        var unrelated = MakePlugin(_c, "C");
        var plugins = new List<LocalPluginInfo> { first, second, unrelated };

        Apply(plugins);

        Assert.False(first.CanLoad);
        Assert.False(second.CanLoad);
        Assert.True(unrelated.CanLoad);
        Assert.Contains("cycle", first.CannotLoadReason);
        Assert.Null(unrelated.CannotLoadReason);
    }

    [Fact]
    public void AbsentOptionalDependency_StillLoads()
    {
        var dependent = MakePlugin(_b, "B", dependencies: Optionally(_a));
        var plugins = new List<LocalPluginInfo> { dependent };

        Apply(plugins);

        Assert.True(dependent.CanLoad);
    }

    [Fact]
    public void OutOfRangeOptionalDependency_IsTreatedAsAbsent()
    {
        var dependency = MakePlugin(_a, "A", version: "1.5.0");
        var dependent = MakePlugin(_b, "B", dependencies: Optionally(_a, "^2.0.0"));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };

        Apply(plugins);

        Assert.True(dependent.CanLoad);
    }

    [Fact]
    public void UnloadableDependency_RefusesItsDependent()
    {
        var dependency = MakePlugin(_a, "A", canLoad: false);
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };

        Apply(plugins);

        Assert.False(dependent.CanLoad);
        Assert.Contains("cannot be loaded", dependent.CannotLoadReason);
    }

    [Fact]
    public void AnEarlierReason_IsKeptInFrontOfTheDependencyReason()
    {
        var dependent = MakePlugin(_b, "B", canLoad: false, dependencies: Requires(_a));
        dependent.CannotLoadReason = "The plugin was built for another runtime.";
        var plugins = new List<LocalPluginInfo> { dependent };

        Apply(plugins);

        Assert.False(dependent.CanLoad);
        Assert.StartsWith("The plugin was built for another runtime.", dependent.CannotLoadReason);
        Assert.Contains("not installed", dependent.CannotLoadReason);
    }

    [Fact]
    public void UnconstrainedPlugins_KeepTheirExistingOrder()
    {
        var first = MakePlugin(_a, "A");
        var second = MakePlugin(_b, "B");
        var third = MakePlugin(_c, "C");
        var plugins = new List<LocalPluginInfo> { first, second, third };

        Apply(plugins);

        Assert.Equal([first, second, third], plugins);
        Assert.Equal([0, 1, 2], new[] { first.LoadOrder, second.LoadOrder, third.LoadOrder });
    }

    [Fact]
    public void DisablingARequiredDependency_RefusesTheLoadedDependentUntilARestart()
    {
        var dependency = MakePlugin(_a, "A", isActive: true);
        var dependent = MakePlugin(_b, "B", isActive: true, dependencies: Requires(_a));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        dependency.IsEnabled = false;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.False(dependent.CanLoad);
        Assert.Contains("disabled", dependent.CannotLoadReason);
        Assert.True(dependent.RestartPending);
        Assert.True(dependency.RestartPending);
        Assert.True(RestartReasonTracker.HasPendingStateChange([dependent]));
    }

    [Fact]
    public void EnablingTheDependencyAgain_TakesTheRefusalBack()
    {
        var dependency = MakePlugin(_a, "A", isActive: true);
        var dependent = MakePlugin(_b, "B", isActive: true, dependencies: Requires(_a));
        var outer = MakePlugin(_c, "C", isActive: true, dependencies: Requires(_b));
        var plugins = new List<LocalPluginInfo> { dependency, dependent, outer };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        dependency.IsEnabled = false;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);
        Assert.False(outer.CanLoad);

        dependency.IsEnabled = true;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.All(plugins, pluginInfo => Assert.True(pluginInfo.CanLoad));
        Assert.All(plugins, pluginInfo => Assert.Null(pluginInfo.CannotLoadReason));
        Assert.All(plugins, pluginInfo => Assert.False(pluginInfo.RestartPending));
        Assert.Empty(refusals);
    }

    [Fact]
    public void EnablingTheDependencyOfAPluginRefusedAtStart_LetsItLoadAfterARestart()
    {
        var dependency = MakePlugin(_a, "A", isEnabled: false);
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);
        Assert.False(dependent.RestartPending);

        dependency.IsEnabled = true;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.True(dependent.CanLoad);
        Assert.Null(dependent.CannotLoadReason);
        Assert.True(dependent.RestartPending);
    }

    [Fact]
    public void TakingARefusalBack_KeepsTheEarlierReason()
    {
        var dependency = MakePlugin(_a, "A");
        var dependent = MakePlugin(_b, "B", canLoad: false, dependencies: Requires(_a));
        dependent.CannotLoadReason = "The plugin was built for another runtime.";
        var plugins = new List<LocalPluginInfo> { dependency, dependent };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        dependency.IsEnabled = false;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);
        Assert.Contains("disabled", dependent.CannotLoadReason);

        dependency.IsEnabled = true;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.False(dependent.CanLoad);
        Assert.Equal("The plugin was built for another runtime.", dependent.CannotLoadReason);
    }

    [Fact]
    public void ADisabledDependent_IsNotRefused()
    {
        var dependency = MakePlugin(_a, "A", isActive: true);
        var dependent = MakePlugin(_b, "B", isEnabled: false, dependencies: Requires(_a));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        dependency.IsEnabled = false;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.True(dependent.CanLoad);
        Assert.False(dependent.RestartPending);
    }

    [Fact]
    public void AnUpdateInstalledNextToTheLoadedVersion_SatisfiesANewerRange()
    {
        var loaded = MakePlugin(_a, "A", isActive: true);
        var plugins = new List<LocalPluginInfo> { loaded };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        plugins.Add(MakePlugin(_a, "A", version: "2.0.0"));
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a, ">=2.0.0"));
        plugins.Add(dependent);
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.True(dependent.CanLoad);
        Assert.Null(dependent.CannotLoadReason);
        Assert.True(dependent.RestartPending);
    }

    [Fact]
    public void AnUpdatePastTheRange_RefusesTheLoadedDependent()
    {
        var loaded = MakePlugin(_a, "A", isActive: true);
        var dependent = MakePlugin(_b, "B", isActive: true, dependencies: Requires(_a, "^1.0.0"));
        var plugins = new List<LocalPluginInfo> { loaded, dependent };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        plugins.Add(MakePlugin(_a, "A", version: "2.0.0"));
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.False(dependent.CanLoad);
        Assert.Contains("version 2.0.0", dependent.CannotLoadReason);
        Assert.True(dependent.RestartPending);
    }

    [Fact]
    public void APinnedOlderVersion_IsTheOneJudged()
    {
        var pinned = MakePlugin(_a, "A", isActive: true);
        pinned.IsPinned = true;
        var dependent = MakePlugin(_b, "B", isActive: true, dependencies: Requires(_a, "^1.0.0"));
        var plugins = new List<LocalPluginInfo> { MakePlugin(_a, "A", version: "2.0.0"), pinned, dependent };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();

        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.True(dependent.CanLoad);
        Assert.False(dependent.RestartPending);
    }

    [Fact]
    public void AnUninstalledDependency_IsNotInstalled()
    {
        var dependency = MakePlugin(_a, "A", isActive: true);
        var dependent = MakePlugin(_b, "B", isActive: true, dependencies: Requires(_a));
        var plugins = new List<LocalPluginInfo> { dependency, dependent };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        dependency.UninstalledAt = DateTime.UnixEpoch;
        dependency.IsEnabled = false;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.False(dependent.CanLoad);
        Assert.Contains("not installed", dependent.CannotLoadReason);
    }

    [Fact]
    public void ReapplyingLeavesTheLoadOrderAlone()
    {
        var dependent = MakePlugin(_b, "B", dependencies: Requires(_a));
        var dependency = MakePlugin(_a, "A");
        var plugins = new List<LocalPluginInfo> { dependent, dependency };
        var refusals = new Dictionary<LocalPluginInfo, PluginManager.PluginLoadState>();
        PluginManager.ApplyDependencyGraph(plugins, refusals, NullLogger.Instance);
        var order = plugins.ToList();

        dependency.IsEnabled = false;
        PluginManager.ReapplyDependencyGraph(plugins, refusals, NullLogger.Instance);

        Assert.Equal(order, plugins);
        Assert.Equal([0, 1], new[] { dependency.LoadOrder, dependent.LoadOrder });
    }
}
