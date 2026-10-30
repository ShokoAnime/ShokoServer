using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Enums;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   What uninstalling or disabling a plugin would do, worked out without
///   doing it: which plugins it uninstalls or disables, which stop loading,
///   and which of the plugin's dependencies stay and why. Made by
///   <see cref="IPluginManager.PlanUninstall"/> and
///   <see cref="IPluginManager.PlanDisable"/>.
/// </summary>
public sealed class PluginChangePlan
{
    /// <summary>
    ///   What the plan is for.
    /// </summary>
    public required PluginChangeKind Kind { get; init; }

    /// <summary>
    ///   The plugin versions asked to be uninstalled or disabled.
    /// </summary>
    public required IReadOnlyList<LocalPluginInfo> Targets { get; init; }

    /// <summary>
    ///   The enabled plugins that depend on a target, required or optional.
    ///   Carrying out the plan while there are any needs it to be forced; the
    ///   <see cref="Entries"/> describe what happens then.
    /// </summary>
    public required IReadOnlyList<LocalPluginInfo> Dependents { get; init; }

    /// <summary>
    ///   Every plugin version the plan touches or looked at: the ones it
    ///   changes first, then the dependencies of the targets that stay.
    /// </summary>
    public required IReadOnlyList<PluginChangePlanEntry> Entries { get; init; }

    /// <summary>
    ///   Indicates carrying out the plan needs it to be forced, as other
    ///   enabled plugins depend on a target.
    /// </summary>
    public bool RequiresForce => Dependents.Count > 0;
}
