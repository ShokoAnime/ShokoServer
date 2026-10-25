using System;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
/// Contains information about a <see cref="IReleaseInfoProvider"/>.
/// </summary>
public class ReleaseProviderInfo
{
    /// <summary>
    /// The unique ID of the provider.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    /// The version of the release info provider.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// The display name of the release info provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Describes what the release info provider is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The <see cref="IReleaseInfoProvider"/> that this info is for.
    /// </summary>
    public required IReleaseInfoProvider Provider { get; init; }

    /// <summary>
    /// Information about the configuration that the release info provider uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    /// Information about the plugin that the release info provider belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }

    /// <summary>
    /// Whether or not the provider is enabled for automatic usage.
    /// </summary>
    public required bool Enabled { get; set; }

    /// <summary>
    /// The priority of the provider during automatic usage.
    /// </summary>
    public required int Priority { get; set; }

    /// <summary>
    ///   Whether the provider has what it needs to answer, such as an API key
    ///   or credentials.
    /// </summary>
    public bool IsConfigured { get => Provider.IsConfigured; }

    /// <summary>
    ///   What the provider is missing while <see cref="IsConfigured"/> is
    ///   <c>false</c>, or <c>null</c>.
    /// </summary>
    public string? NotConfiguredReason { get => Provider.IsConfigured ? null : Provider.NotConfiguredReason; }

    /// <summary>
    ///   How many of the core's jobs for the provider may run at once, or
    ///   <c>null</c> for no limit of its own.
    /// </summary>
    public int? MaxConcurrentJobs { get => Provider.MaxConcurrentJobs; }
}

