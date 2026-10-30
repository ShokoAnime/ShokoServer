using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   The plans for uninstalling and disabling a plugin: what goes, what is
///   disabled, what stops loading, and what stays and why.
/// </summary>
public class PluginChangePlannerTests
{
    private static readonly Guid _meta = Guid.Parse("00000000-0000-4000-8000-000000000001");

    private static readonly Guid _memberA = Guid.Parse("00000000-0000-4000-8000-000000000002");

    private static readonly Guid _memberB = Guid.Parse("00000000-0000-4000-8000-000000000003");

    private static readonly Guid _shared = Guid.Parse("00000000-0000-4000-8000-000000000004");

    private static readonly Guid _other = Guid.Parse("00000000-0000-4000-8000-000000000005");

    #region Helpers

    private static PluginDependency Requires(Guid id, bool optional = false)
        => new() { PluginID = id, VersionRange = ">=1.0.0", IsOptional = optional };

    private static LocalPluginInfo Local(Guid id, bool isDependency, bool isEnabled = true, bool isPinned = false, bool isKept = false, params PluginDependency[] dependencies)
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
            IsEnabled = isEnabled,
            IsActive = isEnabled,
            IsDependency = isDependency,
            IsEnabledOnDemand = isDependency,
            IsPinned = isPinned,
            IsKept = isKept,
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

    /// <summary>
    ///   The enabled states the scan leaves: one of <c>true</c> for each
    ///   plugin installed on purpose, none for those installed as
    ///   dependencies.
    /// </summary>
    private static Dictionary<string, bool> States(IEnumerable<LocalPluginInfo> plugins)
        => plugins.Where(info => !info.IsDependency).ToDictionary(info => info.ID.ToString(), info => info.IsEnabled);

    private static PluginChangePlanEntry Entry(PluginChangePlan plan, Guid id)
        => Assert.Single(plan.Entries, entry => entry.Plugin.ID == id);

    #endregion

    #region Uninstall

    [Fact]
    public void Uninstall_MetaPlugin_TakesItsOrphansAndKeepsWhatOthersNeed()
    {
        var meta = Local(_meta, isDependency: false, dependencies: [Requires(_memberA), Requires(_memberB), Requires(_shared), Requires(_other)]);
        List<LocalPluginInfo> plugins =
        [
            meta,
            Local(_memberA, isDependency: true),
            Local(_memberB, isDependency: true, isPinned: true),
            Local(_shared, isDependency: true),
            Local(_other, isDependency: false),
            Local(Guid.NewGuid(), isDependency: false, dependencies: Requires(_shared)),
        ];

        var plan = PluginChangePlanner.PlanUninstall(plugins, States(plugins), meta, allVersions: false, keepOrphans: false);

        Assert.False(plan.RequiresForce);
        Assert.Equal((PluginChange.Uninstall, PluginChangeReason.Requested), (Entry(plan, _meta).Change, Entry(plan, _meta).Reason));
        Assert.Equal((PluginChange.Uninstall, PluginChangeReason.Orphaned), (Entry(plan, _memberA).Change, Entry(plan, _memberA).Reason));
        Assert.Equal((PluginChange.None, PluginChangeReason.Pinned), (Entry(plan, _memberB).Change, Entry(plan, _memberB).Reason));
        Assert.Equal((PluginChange.None, PluginChangeReason.NeededByOthers), (Entry(plan, _shared).Change, Entry(plan, _shared).Reason));
        Assert.Equal((PluginChange.None, PluginChangeReason.InstalledOnPurpose), (Entry(plan, _other).Change, Entry(plan, _other).Reason));
    }

    [Fact]
    public void Uninstall_KeepingOrphans_KeepsThemInstalled()
    {
        var meta = Local(_meta, isDependency: false, dependencies: Requires(_memberA));
        List<LocalPluginInfo> plugins = [meta, Local(_memberA, isDependency: true)];

        var plan = PluginChangePlanner.PlanUninstall(plugins, States(plugins), meta, allVersions: false, keepOrphans: true);

        Assert.Equal((PluginChange.None, PluginChangeReason.OrphansKept), (Entry(plan, _memberA).Change, Entry(plan, _memberA).Reason));
    }

    [Fact]
    public void Uninstall_WithDependents_NeedsForceAndTakesThem()
    {
        var library = Local(_shared, isDependency: false);
        List<LocalPluginInfo> plugins = [library, Local(_other, isDependency: false, dependencies: Requires(_shared, optional: true))];

        var plan = PluginChangePlanner.PlanUninstall(plugins, States(plugins), library, allVersions: false, keepOrphans: false);

        Assert.True(plan.RequiresForce);
        Assert.Equal(_other, Assert.Single(plan.Dependents).ID);
        Assert.Equal((PluginChange.Uninstall, PluginChangeReason.Dependent), (Entry(plan, _other).Change, Entry(plan, _other).Reason));
    }

    #endregion

    #region Disable

    [Fact]
    public void Disable_MetaPlugin_DisablesTheMembersNothingElseNeeds()
    {
        var meta = Local(_meta, isDependency: false, dependencies: [Requires(_memberA), Requires(_shared)]);
        List<LocalPluginInfo> plugins =
        [
            meta,
            Local(_memberA, isDependency: true),
            Local(_shared, isDependency: true),
            Local(_other, isDependency: false, dependencies: Requires(_shared)),
        ];

        var plan = PluginChangePlanner.PlanDisable(plugins, States(plugins), meta);

        Assert.Equal(PluginChange.Disable, Entry(plan, _meta).Change);
        Assert.Equal((PluginChange.Disable, PluginChangeReason.NoLongerNeeded), (Entry(plan, _memberA).Change, Entry(plan, _memberA).Reason));
        var shared = Entry(plan, _shared);
        Assert.Equal((PluginChange.None, PluginChangeReason.NeededByOthers), (shared.Change, shared.Reason));
        Assert.Equal([_other], shared.Related);
        Assert.False(plan.RequiresForce);
    }

    [Fact]
    public void Disable_KeepingDependencies_LeavesTheMembersEnabled()
    {
        var meta = Local(_meta, isDependency: false, dependencies: Requires(_memberA));
        List<LocalPluginInfo> plugins = [meta, Local(_memberA, isDependency: true)];

        var plan = PluginChangePlanner.PlanDisable(plugins, States(plugins), meta, keepDependencies: true);

        Assert.Equal(PluginChange.Disable, Entry(plan, _meta).Change);
        Assert.Equal((PluginChange.None, PluginChangeReason.InstalledOnPurpose), (Entry(plan, _memberA).Change, Entry(plan, _memberA).Reason));
    }

    [Fact]
    public void Disable_PluginEnabledOnDemand_StaysEnabledWhileNeeded()
    {
        var shared = Local(_shared, isDependency: true);
        List<LocalPluginInfo> plugins = [shared, Local(_other, isDependency: false, dependencies: Requires(_shared))];

        var plan = PluginChangePlanner.PlanDisable(plugins, States(plugins), shared);

        var entry = Entry(plan, _shared);
        Assert.Equal((PluginChange.None, PluginChangeReason.NeededByOthers), (entry.Change, entry.Reason));
        Assert.Equal([_other], entry.Related);
    }

    [Fact]
    public void Disable_RequiredDependency_StopsItsDependentLoading()
    {
        var library = Local(_shared, isDependency: false);
        List<LocalPluginInfo> plugins = [library, Local(_other, isDependency: false, dependencies: Requires(_shared))];

        var plan = PluginChangePlanner.PlanDisable(plugins, States(plugins), library);

        Assert.True(plan.RequiresForce);
        var dependent = Entry(plan, _other);
        Assert.Equal((PluginChange.StopLoading, PluginChangeReason.MissingDependency), (dependent.Change, dependent.Reason));
        Assert.Equal([_shared], dependent.Related);
    }

    #endregion
}
