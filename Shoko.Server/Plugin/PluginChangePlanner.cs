using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.Plugin;

/// <summary>
///   Works out what uninstalling or disabling a plugin would do, on a copy of
///   the state, by the same orphan, demand and dependents rules the actions
///   follow, so a client can show it before asking.
/// </summary>
internal static class PluginChangePlanner
{
    /// <summary>
    ///   Works out what uninstalling a plugin would do. It follows the
    ///   uninstall routes: the dependents of the active version are
    ///   uninstalled first when forced, then the version or versions asked
    ///   for, then the plugins orphaned since, apart from their pinned and kept
    ///   versions, unless the orphans are kept.
    /// </summary>
    /// <param name="plugins">Every registered plugin version.</param>
    /// <param name="enabledStates">The enabled state of each plugin, by DLL name.</param>
    /// <param name="pluginInfo">The plugin version to uninstall.</param>
    /// <param name="allVersions">Uninstall every installed version of the plugin.</param>
    /// <param name="keepOrphans">Keep the plugins orphaned by the uninstall.</param>
    /// <returns>The plan.</returns>
    internal static PluginChangePlan PlanUninstall(
        IReadOnlyList<LocalPluginInfo> plugins,
        IReadOnlyDictionary<string, bool> enabledStates,
        LocalPluginInfo pluginInfo,
        bool allVersions,
        bool keepOrphans
    )
    {
        var installed = plugins.Where(info => info.IsInstalled).ToList();
        var targets = allVersions ? installed.Where(info => info.ID == pluginInfo.ID).ToList() : [pluginInfo];
        // The routes take the cascade from the version asked for, or from the active one when
        // uninstalling every version, and only from an active one.
        var cascadeFrom = allVersions ? targets.FirstOrDefault(info => info.IsActive) : pluginInfo;
        var dependents = cascadeFrom is null ? [] : FindDependents(plugins, cascadeFrom.ID, transitive: true);

        var state = new PlanState(installed, enabledStates);
        var entries = new List<PluginChangePlanEntry>();
        void Uninstall(LocalPluginInfo info, PluginChangeReason reason)
        {
            if (!info.CanUninstall || !info.IsInstalled)
            {
                entries.Add(new() { Plugin = info, Change = PluginChange.None, Reason = PluginChangeReason.CannotUninstall });
                return;
            }

            state.Uninstall(info);
            entries.Add(new() { Plugin = info, Change = PluginChange.Uninstall, Reason = reason });
        }

        var orphansBefore = PluginManager.FindOrphans(installed);
        foreach (var dependent in dependents)
            Uninstall(dependent, PluginChangeReason.Dependent);
        foreach (var target in targets)
            Uninstall(target, PluginChangeReason.Requested);

        var newOrphans = PluginManager.FindOrphans(state.Installed).Except(orphansBefore).ToHashSet();
        foreach (var orphan in state.Installed.Where(info => newOrphans.Contains(info.ID)).ToList())
        {
            if (keepOrphans)
                entries.Add(new() { Plugin = orphan, Change = PluginChange.None, Reason = PluginChangeReason.OrphansKept });
            else if (orphan.IsPinned)
                entries.Add(new() { Plugin = orphan, Change = PluginChange.None, Reason = PluginChangeReason.Pinned });
            else if (orphan.IsKept)
                entries.Add(new() { Plugin = orphan, Change = PluginChange.None, Reason = PluginChangeReason.Kept });
            else
                Uninstall(orphan, PluginChangeReason.Orphaned);
        }

        AddFollowOnChanges(installed, state, entries);
        AddStayingDependencies(targets, state, entries, enabledOnly: false);
        return new()
        {
            Kind = PluginChangeKind.Uninstall,
            Targets = targets,
            Dependents = dependents,
            Entries = entries,
        };
    }

    /// <summary>
    ///   Works out what disabling a plugin would do. It follows
    ///   <see cref="PluginManager.DisablePlugin"/>: every version of the plugin is disabled,
    ///   and an enabled state of <c>false</c> is kept only where the plugin had
    ///   one of <c>true</c>, so a plugin enabled on demand stays enabled while
    ///   an enabled plugin needs it.
    /// </summary>
    /// <param name="plugins">Every registered plugin version.</param>
    /// <param name="enabledStates">The enabled state of each plugin, by DLL name.</param>
    /// <param name="pluginInfo">The plugin to disable.</param>
    /// <param name="keepDependencies">
    ///   Mark the plugin's direct dependencies as installed on purpose first,
    ///   as <see cref="PluginManager.UnmarkAsDependency"/> does, so they keep the state they
    ///   have.
    /// </param>
    /// <returns>The plan.</returns>
    internal static PluginChangePlan PlanDisable(
        IReadOnlyList<LocalPluginInfo> plugins,
        IReadOnlyDictionary<string, bool> enabledStates,
        LocalPluginInfo pluginInfo,
        bool keepDependencies = false
    )
    {
        var installed = plugins.Where(info => info.IsInstalled).ToList();
        var dependents = FindDependents(plugins, pluginInfo.ID, transitive: false);
        var state = new PlanState(installed, enabledStates);
        if (keepDependencies)
            foreach (var dependency in pluginInfo.Dependencies)
                foreach (var info in installed.Where(info => info.ID == dependency.PluginID))
                    state.Unmark(info);
        state.Disable(pluginInfo);

        var entries = new List<PluginChangePlanEntry>();
        if (state.IsEnabledAfter(pluginInfo))
            entries.Add(new()
            {
                Plugin = pluginInfo,
                Change = PluginChange.None,
                Reason = PluginChangeReason.NeededByOthers,
                Related = state.DependentsAfter(pluginInfo.ID, enabledOnly: true),
            });
        else
            entries.Add(new() { Plugin = pluginInfo, Change = PluginChange.Disable, Reason = PluginChangeReason.Requested });

        AddFollowOnChanges(installed, state, entries);
        AddStayingDependencies([pluginInfo], state, entries, enabledOnly: true);
        return new()
        {
            Kind = PluginChangeKind.Disable,
            Targets = [pluginInfo],
            Dependents = dependents,
            Entries = entries,
        };
    }

    /// <summary>
    ///   Adds what follows from the change: the plugins enabled on demand that
    ///   no enabled plugin needs anymore, and the ones that stop loading as a
    ///   required dependency is gone.
    /// </summary>
    private static void AddFollowOnChanges(IReadOnlyList<LocalPluginInfo> installed, PlanState state, List<PluginChangePlanEntry> entries)
    {
        var listed = entries.Select(entry => entry.Plugin).ToHashSet();
        foreach (var info in state.Installed)
        {
            if (listed.Contains(info) || !info.IsEnabled || state.IsEnabledAfter(info))
                continue;

            entries.Add(new() { Plugin = info, Change = PluginChange.Disable, Reason = PluginChangeReason.NoLongerNeeded });
            listed.Add(info);
        }

        var refusedBefore = FindRefusals(installed.Where(info => info.IsEnabled));
        var refusedAfter = FindRefusals(state.Installed.Where(state.IsEnabledAfter));
        foreach (var (info, missing) in refusedAfter)
        {
            if (refusedBefore.Keys.Any(other => other.ID == info.ID) || listed.Contains(info))
                continue;

            entries.Add(new() { Plugin = info, Change = PluginChange.StopLoading, Reason = PluginChangeReason.MissingDependency, Related = missing });
            listed.Add(info);
        }
    }

    /// <summary>
    ///   Adds the dependencies of the targets, direct or through others, that
    ///   stay, with why: installed on purpose, or needed by other plugins.
    /// </summary>
    /// <param name="targets">The targets.</param>
    /// <param name="state">The state once the plan is carried out.</param>
    /// <param name="entries">The entries so far.</param>
    /// <param name="enabledOnly">Only count enabled plugins as needing a dependency.</param>
    private static void AddStayingDependencies(IReadOnlyList<LocalPluginInfo> targets, PlanState state, List<PluginChangePlanEntry> entries, bool enabledOnly)
    {
        var listed = entries.Select(entry => entry.Plugin.ID).ToHashSet();
        var seen = targets.Select(target => target.ID).ToHashSet();
        var pending = new Queue<PluginDependency>(targets.SelectMany(target => target.Dependencies));
        while (pending.TryDequeue(out var dependency))
        {
            if (!seen.Add(dependency.PluginID))
                continue;

            foreach (var info in state.Installed.Where(info => info.ID == dependency.PluginID))
            {
                foreach (var next in info.Dependencies)
                    pending.Enqueue(next);
                if (listed.Contains(info.ID) || (enabledOnly && !state.IsEnabledAfter(info)))
                    continue;

                entries.Add(state.IsDependencyAfter(info)
                    ? new()
                    {
                        Plugin = info,
                        Change = PluginChange.None,
                        Reason = PluginChangeReason.NeededByOthers,
                        Related = state.DependentsAfter(info.ID, enabledOnly),
                    }
                    : new() { Plugin = info, Change = PluginChange.None, Reason = PluginChangeReason.InstalledOnPurpose });
            }
        }
    }

    /// <summary>
    ///   The active, installed plugins that depend on a plugin, required or
    ///   optional, as the dependency resolver finds them: directly, or with
    ///   the dependents of the dependents too.
    /// </summary>
    private static List<LocalPluginInfo> FindDependents(IReadOnlyList<LocalPluginInfo> plugins, Guid pluginID, bool transitive)
    {
        var candidates = plugins.Where(info => info.IsActive && info.IsInstalled && info.ID != pluginID).ToList();
        var found = new List<LocalPluginInfo>();
        var visited = new HashSet<Guid>();
        var pending = new Queue<Guid>([pluginID]);
        while (pending.TryDequeue(out var id))
        {
            foreach (var candidate in candidates)
            {
                if (visited.Contains(candidate.ID) || candidate.Dependencies.All(dependency => dependency.PluginID != id))
                    continue;

                visited.Add(candidate.ID);
                found.Add(candidate);
                if (transitive)
                    pending.Enqueue(candidate.ID);
            }
        }

        return found;
    }

    /// <summary>
    ///   The plugins that would be refused for a required dependency that is
    ///   missing, out of range or refused itself, judging each plugin by the
    ///   version the next start picks, with the dependencies each misses.
    /// </summary>
    private static Dictionary<LocalPluginInfo, IReadOnlyList<Guid>> FindRefusals(IEnumerable<LocalPluginInfo> enabled)
    {
        var picked = new Dictionary<Guid, LocalPluginInfo>();
        foreach (var info in enabled)
            if (!picked.TryGetValue(info.ID, out var current) || (info.IsPinned != current.IsPinned ? info.IsPinned : info.Version.Version > current.Version.Version))
                picked[info.ID] = info;

        var refused = new Dictionary<Guid, IReadOnlyList<Guid>>();
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var (id, info) in picked)
            {
                if (refused.ContainsKey(id))
                    continue;

                var missing = info.Dependencies
                    .Where(dependency => !dependency.IsOptional && !(
                        picked.TryGetValue(dependency.PluginID, out var target) &&
                        PluginVersionRange.IsSatisfied(dependency.VersionRange, target.Version.Version) &&
                        !refused.ContainsKey(dependency.PluginID)
                    ))
                    .Select(dependency => dependency.PluginID)
                    .ToList();
                if (missing.Count is 0)
                    continue;

                refused[id] = missing;
                changed = true;
            }
        }

        return refused.ToDictionary(pair => picked[pair.Key], pair => pair.Value);
    }

    /// <summary>
    ///   A copy of the installed plugins and their enabled states that a plan
    ///   changes, and what the demand pass makes of it.
    /// </summary>
    private sealed class PlanState(IReadOnlyList<LocalPluginInfo> installed, IReadOnlyDictionary<string, bool> enabledStates)
    {
        private readonly HashSet<LocalPluginInfo> _uninstalled = [];

        private readonly HashSet<LocalPluginInfo> _disabled = [];

        private readonly HashSet<LocalPluginInfo> _unmarked = [];

        private readonly Dictionary<string, bool> _enabledStates = new(enabledStates);

        private (IReadOnlySet<Guid> OnDemand, IReadOnlySet<Guid> Demanded)? _demand;

        public IReadOnlyList<LocalPluginInfo> Installed => installed.Where(info => !_uninstalled.Contains(info)).ToList();

        /// <summary>
        ///   Uninstalls the version: it is disabled, and the plugin loses its
        ///   enabled state.
        /// </summary>
        public void Uninstall(LocalPluginInfo info)
        {
            _uninstalled.Add(info);
            _enabledStates.Remove(PluginManager.GetDllName(info));
            _demand = null;
        }

        /// <summary>
        ///   Disables every version of the plugin; its enabled state turns to
        ///   <c>false</c> only where it was <c>true</c>.
        /// </summary>
        public void Disable(LocalPluginInfo info)
        {
            var dllName = PluginManager.GetDllName(info);
            if (_enabledStates.TryGetValue(dllName, out var enabled) && enabled)
                _enabledStates[dllName] = false;
            foreach (var version in installed.Where(other => other.ID == info.ID))
                _disabled.Add(version);
            _disabled.Add(info);
            _demand = null;
        }

        /// <summary>
        ///   Marks the version as installed on purpose; a plugin enabled on
        ///   demand gets the enabled state it has now.
        /// </summary>
        public void Unmark(LocalPluginInfo info)
        {
            if (!info.IsDependency)
                return;

            _unmarked.Add(info);
            if (info.IsEnabledOnDemand)
                _enabledStates.TryAdd(PluginManager.GetDllName(info), info.IsEnabled);
            _demand = null;
        }

        /// <summary>
        ///   Whether the version is marked as installed as a dependency once
        ///   the plan is carried out.
        /// </summary>
        public bool IsDependencyAfter(LocalPluginInfo info)
            => info.IsDependency && !_unmarked.Contains(info);

        private bool IsEnabledBeforeDemand(LocalPluginInfo info)
            => info.IsEnabled && !_disabled.Contains(info) && !_uninstalled.Contains(info);

        /// <summary>
        ///   Whether the version is enabled once the plan is carried out, after
        ///   the demand pass.
        /// </summary>
        public bool IsEnabledAfter(LocalPluginInfo info)
        {
            if (_uninstalled.Contains(info))
                return false;

            var (onDemand, demanded) = _demand ??= PluginManager.FindDemand(
                Installed.Select(other => new PluginManager.DemandNode(other.ID, PluginManager.GetDllName(other), IsDependencyAfter(other), IsEnabledBeforeDemand(other), other.IsPinned, other.Version.Version, other.Dependencies)).ToList(),
                _enabledStates
            );
            return onDemand.Contains(info.ID) ? demanded.Contains(info.ID) : IsEnabledBeforeDemand(info);
        }

        /// <summary>
        ///   The plugins still installed, or enabled, that depend on a plugin
        ///   once the plan is carried out.
        /// </summary>
        public IReadOnlyList<Guid> DependentsAfter(Guid pluginID, bool enabledOnly)
            => Installed
                .Where(info => info.ID != pluginID && (!enabledOnly || IsEnabledAfter(info)) && info.Dependencies.Any(dependency => dependency.PluginID == pluginID))
                .Select(info => info.ID)
                .Distinct()
                .ToList();
    }

}
