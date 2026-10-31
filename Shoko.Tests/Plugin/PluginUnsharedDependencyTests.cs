using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// An optional dependency that does not fit its dependent is left out of the
/// dependent's load context; a required one is never left out.
/// </summary>
public class PluginUnsharedDependencyTests
{
    private static readonly Guid _dependentId = Guid.NewGuid();

    private static readonly Guid _dependencyId = Guid.NewGuid();

    [Fact]
    public void OptionalDependencyOutOfRange_IsNotShared()
    {
        var unshared = Find(Dependent(">=2.0.0", isOptional: true), Dependency("1.0.0"));

        Assert.Equal([_dependencyId], unshared[_dependentId]);
    }

    [Fact]
    public void OptionalDependencyThatDoesNotFit_IsNotShared()
    {
        var unshared = Find(Dependent(">=1.0.0", isOptional: true), Dependency("1.0.0"), mismatch: "missing members");

        Assert.Equal([_dependencyId], unshared[_dependentId]);
    }

    [Fact]
    public void OptionalDependencyThatFits_IsShared()
        => Assert.Empty(Find(Dependent(">=1.0.0", isOptional: true), Dependency("1.0.0")));

    [Fact]
    public void RequiredDependency_IsAlwaysShared()
        => Assert.Empty(Find(Dependent(">=2.0.0", isOptional: false), Dependency("1.0.0"), mismatch: "missing members"));

    private static Dictionary<Guid, HashSet<Guid>> Find(PluginManager.InternalPluginInfo dependent, PluginManager.InternalPluginInfo dependency, string? mismatch = null)
        => PluginManager.FindUnsharedDependencies([dependent, dependency], (_, _) => mismatch, NullLogger.Instance);

    private static PluginManager.InternalPluginInfo Dependent(string range, bool isOptional)
        => Info(_dependentId, "Dependent", "1.0.0", [new() { PluginID = _dependencyId, VersionRange = range, IsOptional = isOptional }]);

    private static PluginManager.InternalPluginInfo Dependency(string version)
        => Info(_dependencyId, "Dependency", version, []);

    private static PluginManager.InternalPluginInfo Info(Guid id, string name, string version, IReadOnlyList<PluginDependency> dependencies)
        => new()
        {
            ID = id,
            Name = name,
            Description = string.Empty,
            Version = new()
            {
                Version = Version.Parse(version),
                RuntimeIdentifier = IPluginManager.AnyRuntimeIdentifier,
                AbstractionVersion = typeof(IPlugin).Assembly.GetName().Version!,
                SourceRevision = null,
                ReleaseTag = null,
                Channel = ReleaseChannel.Stable,
                ReleasedAt = DateTime.UnixEpoch,
            },
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Priority = 0,
            InstalledAt = DateTime.UnixEpoch,
            IsPinned = false,
            IsEnabled = true,
            CanLoad = true,
            CanUninstall = true,
            DllName = name,
            ContainingDirectory = null,
            DLLs = [$"/nonexistent/{name}.dll"],
            Dependencies = dependencies,
        };
}
