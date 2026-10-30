using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Plugin.Models;

using AbstractPluginChangePlan = Shoko.Abstractions.Plugin.Models.PluginChangePlan;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   What uninstalling or disabling a plugin would do, worked out without
///   doing it: which plugins it uninstalls or disables, which stop loading,
///   and which of the plugin's dependencies stay and why.
/// </summary>
public class PluginChangePlan(AbstractPluginChangePlan plan)
{
    /// <summary>
    ///   What the plan is for.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public PluginChangeKind Kind { get; init; } = plan.Kind;

    /// <summary>
    ///   Indicates carrying out the plan needs it to be forced, as other
    ///   enabled plugins depend on a target.
    /// </summary>
    [Required]
    public bool RequiresForce { get; init; } = plan.RequiresForce;

    /// <summary>
    ///   The plugin versions asked to be uninstalled or disabled.
    /// </summary>
    [Required]
    public IReadOnlyList<PluginInfo> Targets { get; init; } = plan.Targets
        .Select(pluginInfo => new PluginInfo(pluginInfo))
        .ToList();

    /// <summary>
    ///   The enabled plugins that depend on a target, required or optional.
    ///   The <see cref="Entries"/> describe what happens to them when the plan
    ///   is forced.
    /// </summary>
    [Required]
    public IReadOnlyList<PluginInfo> Dependents { get; init; } = plan.Dependents
        .Select(pluginInfo => new PluginInfo(pluginInfo))
        .ToList();

    /// <summary>
    ///   Every plugin version the plan touches or looked at: the ones it
    ///   changes first, then the dependencies of the targets that stay.
    /// </summary>
    [Required]
    public IReadOnlyList<Entry> Entries { get; init; } = plan.Entries
        .Select(entry => new Entry(entry))
        .ToList();

    /// <summary>
    ///   One plugin version in the plan.
    /// </summary>
    public class Entry(PluginChangePlanEntry entry)
    {
        /// <summary>
        ///   The plugin version.
        /// </summary>
        [Required]
        public PluginInfo Plugin { get; init; } = new(entry.Plugin);

        /// <summary>
        ///   What happens to it.
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public PluginChange Change { get; init; } = entry.Change;

        /// <summary>
        ///   Why.
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public PluginChangeReason Reason { get; init; } = entry.Reason;

        /// <summary>
        ///   For a plugin that stays because others need it, the plugins that
        ///   still depend on it. For one that stops loading, the required
        ///   dependencies it no longer has.
        /// </summary>
        [Required]
        public IReadOnlyList<Guid> Related { get; init; } = entry.Related;
    }
}
