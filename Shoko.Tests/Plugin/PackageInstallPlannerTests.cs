using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Installing a package plans the dependencies it needs that are not
///   installed yet, and refuses what cannot be satisfied.
/// </summary>
public class PackageInstallPlannerTests
{
    private static readonly Guid _app = Guid.Parse("00000000-0000-4000-8000-00000000000a");

    private static readonly Guid _library = Guid.Parse("00000000-0000-4000-8000-00000000000b");

    private static readonly Guid _core = Guid.Parse("00000000-0000-4000-8000-00000000000c");

    private static readonly Guid _repositoryA = Guid.Parse("00000000-0000-4000-8000-0000000000a1");

    private static readonly Guid _repositoryB = Guid.Parse("00000000-0000-4000-8000-0000000000b1");

    #region Helpers

    private static bool IsCompatible(Version abstractionVersion, string runtimeIdentifier)
        => runtimeIdentifier is IPluginManager.AnyRuntimeIdentifier && abstractionVersion <= new Version(6, 0, 0);

    private static PluginDependency Requires(Guid id, string range, bool optional = false)
        => new() { PluginID = id, VersionRange = range, IsOptional = optional };

    private static PackageReleaseInfo Release(string version, Guid? repositoryID = null, string abstractionVersion = "6.0.0", params PluginDependency[] dependencies)
        => new()
        {
            RepositoryID = repositoryID ?? _repositoryA,
            Version = Version.Parse(version),
            Tag = null,
            SourceRevision = null,
            ReleasedAt = DateTime.UnixEpoch,
            Channel = ReleaseChannel.Stable,
            ReleaseNotes = null,
            Dependencies = dependencies,
            Archives =
            [
                new()
                {
                    RuntimeIdentifier = IPluginManager.AnyRuntimeIdentifier,
                    AbstractionVersion = Version.Parse(abstractionVersion),
                    ArchiveUrl = $"https://repo.test/{version}.zip",
                    ArchiveChecksum = string.Empty,
                },
            ],
        };

    private static PackageManifestInfo Manifest(Guid id, params PackageReleaseInfo[] releases)
        => new()
        {
            PackageID = id,
            Name = id == _app ? "App" : id == _library ? "Library" : "Core",
            Overview = string.Empty,
            Authors = string.Empty,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            Releases = releases,
            LastFetchedAt = DateTime.UnixEpoch,
        };

    private static PackageRepositoryInfo Repository(Guid id)
        => new() { ID = id, Name = id.ToString(), Url = "https://repo.test/manifest.json", StaleTime = null, LastFetchedAt = null };

    private static PackageInfo Package(PackageManifestInfo manifest, int release = 0)
        => new()
        {
            Manifest = manifest,
            Repository = Repository(manifest.Releases[release].RepositoryID ?? _repositoryA),
            Release = manifest.Releases[release],
            Archive = manifest.Releases[release].Archives[0],
            Plugin = null,
        };

    private static InstalledPlugin Installed(Guid id, string version, bool isEnabled = true, bool isPinned = false, bool isEnabledOnDemand = false)
        => new(id, id.ToString(), Version.Parse(version), isEnabled, isPinned, IsEnabledOnDemand: isEnabledOnDemand);

    private static PackageInstallPlan Plan(PackageManifestInfo app, IReadOnlyList<InstalledPlugin> installed, bool includeOptional = false, params PackageManifestInfo[] others)
        => PackageInstallPlanner.Plan(
            Package(app),
            installed,
            [app, .. others],
            [Repository(_repositoryA), Repository(_repositoryB)],
            IsCompatible,
            "linux-x64",
            includeOptional
        );

    private static IReadOnlyList<string> Steps(PackageInstallPlan plan)
        => plan.Steps.Select(step => $"{step.Package.Manifest.Name}@{step.Package.Release.Version}").ToList();

    #endregion

    [Fact]
    public void MissingDependencies_ArePlannedFirst_NewestInRange()
    {
        var app = Manifest(_app, Release("1.0.0", dependencies: Requires(_library, "^1.0.0")));
        var library = Manifest(_library, Release("2.0.0"), Release("1.4.0", dependencies: Requires(_core, ">=1.0.0")), Release("1.2.0"));
        var core = Manifest(_core, Release("1.1.0"));

        var plan = Plan(app, [], others: [library, core]);

        Assert.True(plan.CanInstall);
        Assert.Equal(["Core@1.1.0", "Library@1.4.0", "App@1.0.0"], Steps(plan));
        Assert.Equal([_library], plan.Steps[0].RequiredBy);
        Assert.Equal([true, true, false], plan.Steps.Select(step => step.IsDependency));
    }

    [Fact]
    public void InstalledVersionInRange_SatisfiesDependency()
    {
        var app = Manifest(_app, Release("1.0.0", dependencies: Requires(_library, "^1.0.0")));

        var plan = Plan(app, [Installed(_library, "1.1.0", isEnabled: false, isEnabledOnDemand: true)], others: Manifest(_library, Release("1.4.0")));

        Assert.True(plan.CanInstall);
        Assert.Equal(["App@1.0.0"], Steps(plan));
    }

    [Theory]
    [InlineData(false, false, "disabled")]
    [InlineData(true, true, "pinned")]
    public void InstalledDependency_ThatCannotServe_IsAProblem(bool isEnabled, bool isPinned, string reason)
    {
        var app = Manifest(_app, Release("1.0.0", dependencies: Requires(_library, isPinned ? "^2.0.0" : "^1.0.0")));

        var plan = Plan(app, [Installed(_library, "1.1.0", isEnabled, isPinned)], others: Manifest(_library, Release("2.0.0")));

        Assert.False(plan.CanInstall);
        Assert.Contains(reason, Assert.Single(plan.Problems).Reason);
    }

    [Fact]
    public void UnknownOrIncompatibleDependency_IsAProblem()
    {
        var app = Manifest(_app, Release("1.0.0", dependencies: [Requires(_library, "^1.0.0"), Requires(_core, "^1.0.0")]));

        var plan = Plan(app, [], others: Manifest(_library, Release("1.0.0", abstractionVersion: "7.0.0")));

        Assert.Equal([_library, _core], plan.Problems.Select(problem => problem.PluginID));
    }

    [Fact]
    public void ConflictingRanges_AreAProblem()
    {
        var app = Manifest(_app, Release("1.0.0", dependencies: [Requires(_library, "^1.0.0"), Requires(_core, "^1.0.0")]));
        var library = Manifest(_library, Release("1.0.0", dependencies: Requires(_core, "^2.0.0")));

        var plan = Plan(app, [], others: [library, Manifest(_core, Release("2.0.0"), Release("1.0.0"))]);

        Assert.Equal(_core, Assert.Single(plan.Problems).PluginID);
    }

    [Fact]
    public void Cycle_IsAProblem()
    {
        var app = Manifest(_app, Release("1.0.0", dependencies: Requires(_library, "^1.0.0")));
        var library = Manifest(_library, Release("1.0.0", dependencies: Requires(_app, "^1.0.0")));

        var plan = Plan(app, [], others: library);

        Assert.Contains("cycle", Assert.Single(plan.Problems).Reason);
    }

    [Theory]
    [InlineData(false, new[] { "App@1.0.0" })]
    [InlineData(true, new[] { "Library@1.0.0", "App@1.0.0" })]
    public void OptionalDependency_IsOnlyPlannedWhenAsked(bool includeOptional, string[] expected)
    {
        var app = Manifest(_app, Release("1.0.0", dependencies: Requires(_library, "^1.0.0", optional: true)));

        var plan = Plan(app, [], includeOptional, Manifest(_library, Release("1.0.0")));

        Assert.Equal(expected, Steps(plan));
        Assert.Equal(includeOptional ? 0 : 1, plan.OptionalDependencies.Count);
    }

    [Fact]
    public void SameVersionInTwoRepositories_PrefersTheDependentsRepository()
    {
        var app = Manifest(_app, Release("1.0.0", _repositoryB, dependencies: Requires(_library, "^1.0.0")));

        var plan = Plan(app, [], others: Manifest(_library, Release("1.0.0", _repositoryA), Release("1.0.0", _repositoryB)));

        Assert.Equal(_repositoryB, plan.Steps[0].Package.Release.RepositoryID);
    }
}
