using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;

using AbstractRestartReason = Shoko.Abstractions.Core.RestartReason;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// One reason the server needs a restart for a change to take effect.
/// </summary>
public class RestartReason
{
    /// <summary>
    /// Where the reason came from: <c>Configuration</c>, <c>PluginState</c> or <c>Plugin</c>.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public RestartReasonSource Source { get; init; }

    /// <summary>
    /// The plugin the reason belongs to: the core plugin for the <c>Configuration</c> and
    /// <c>PluginState</c> reasons, and the plugin that raised it for a <c>Plugin</c> reason.
    /// </summary>
    [Required]
    public Guid PluginID { get; init; }

    /// <summary>
    /// The plugin's name, if it is still known.
    /// </summary>
    public string? PluginName { get; init; }

    /// <summary>
    /// A key that stays the same while the reason stands, unique within its source and plugin:
    /// <c>configuration</c>, <c>plugins</c>, or a generated ID for a <c>Plugin</c> reason.
    /// </summary>
    [Required]
    public string Key { get; init; }

    /// <summary>
    /// A short, human-readable description of what changed.
    /// </summary>
    [Required]
    public string Description { get; init; }

    /// <summary>
    /// When the reason was first raised, in UTC.
    /// </summary>
    [Required]
    public DateTime RaisedAt { get; init; }

    /// <summary>
    /// Builds the model for one reason, resolving the plugin name.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="pluginManager">The plugin manager, to name the plugin.</param>
    public RestartReason(AbstractRestartReason reason, IPluginManager pluginManager)
    {
        Source = reason.Source;
        PluginID = reason.PluginID;
        PluginName = pluginManager.GetPluginInfo(reason.PluginID)?.Name;
        Key = reason.Key;
        Description = reason.Description;
        RaisedAt = reason.RaisedAt.ToUniversalTime();
    }
}
