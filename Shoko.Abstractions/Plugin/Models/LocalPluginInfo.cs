using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Utilities;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Information about a plugin.
/// </summary>
public sealed class LocalPluginInfo
{
    /// <summary>
    ///   The unique identifier for the plugin.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The name of the plugin.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The description of the plugin.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   A short, unique name the plugin reserves for itself, or <c>null</c>
    ///   when it claims none. Other plugins and conventions may key off it;
    ///   for example, a frontend plugin might prefix the parts a plugin adds
    ///   to it with this name.
    /// </summary>
    /// <remarks>
    ///   Stamped into the plugin's assembly as <c>PackageNamespace</c> by the
    ///   build tools, from the project's <c>PluginNamespace</c> or the
    ///   manifest's <c>namespace</c>. It is kebab-case
    ///   (<c>^[a-z][a-z0-9]*(-[a-z0-9]+)*$</c>), 2 to 32 characters long, and
    ///   not <c>shoko</c>, <c>core</c> or <c>plugin</c>. A plugin breaking
    ///   these rules, or claiming the namespace of another loaded plugin,
    ///   fails the start, and the value is then left out.
    /// </remarks>
    public string? Namespace { get; init; }

    /// <summary>
    ///   Whether the plugin is a meta plugin, one whose only code is its
    ///   generated plugin class, so it only brings in its dependencies, or
    ///   <c>null</c> when its build did not say.
    /// </summary>
    /// <remarks>
    ///   Detected by the build tools and stamped into the plugin's assembly
    ///   as <c>PackageIsMeta</c>; no project sets it by hand.
    /// </remarks>
    public bool? IsMeta { get; init; }

    /// <summary>
    ///   The version of the plugin.
    /// </summary>
    public required VersionInformation Version { get; init; }

    /// <summary>
    ///   The author(s) of the plugin.
    /// </summary>
    public required string? Authors { get; init; }

    /// <summary>
    ///   The repository URL for the plugin, if provided.
    /// </summary>
    public required string? RepositoryUrl { get; init; }

    /// <summary>
    ///   The home-page URL for the plugin, if provided.
    /// </summary>
    public required string? HomepageUrl { get; init; }

    /// <summary>
    ///   The search tags for the plugin. A maximum of 10 tags will be loaded if
    ///   provided.
    /// </summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>
    ///   The order in which the plugin was loaded. Dependencies are always
    ///   ordered before the plugins depending upon them.
    /// </summary>
    public int LoadOrder { get; internal set; }

    /// <summary>
    ///   The thumbnail for the plugin, if it is available for the plugin.
    /// </summary>
    public required PackageImageInfo? Thumbnail { get; init; }

    /// <summary>
    ///   The icon for the plugin, if it is available for the plugin. Square,
    ///   and meant for the places the thumbnail is too wide for.
    /// </summary>
    public required PackageImageInfo? Icon { get; init; }

    /// <summary>
    ///   When the plugin was installed to the local system: the last-write
    ///   time of its main DLL, in UTC. A package install sets it to the
    ///   install time; a manually copied plugin keeps whatever time the copy
    ///   gave the file.
    /// </summary>
    public required DateTime InstalledAt { get; init; }

    /// <summary>
    /// When the plugin was uninstalled from the local system.
    /// </summary>
    public DateTime? UninstalledAt { get; internal set; }

    /// <summary>
    ///   Indicates the plugin is currently installed. Will be <c>false</c>
    ///   if the plugin has been uninstalled in the current session, or if it's
    ///   a remote plugin that has not yet been installed.
    /// </summary>
    public bool IsInstalled => !UninstalledAt.HasValue;

    /// <summary>
    ///   Indicates the plugin is currently enabled for use in the current
    ///   session or for the next session.
    /// </summary>
    public bool IsEnabled { get; internal set; }

    /// <summary>
    ///   Indicates the current version of the plugin is pinned.
    /// </summary>
    public bool IsPinned { get; internal set; }

    /// <summary>
    ///   Indicates a <c>.keep</c> file, in the plugin's directory or next to
    ///   a single-DLL plugin, protects this version from
    ///   <see cref="IPluginPackageManager.PurgeInactivePluginVersions"/>. It
    ///   can still be uninstalled, and unlike pinning it does not make the
    ///   version the active one.
    /// </summary>
    public bool IsKept { get; internal set; }

    /// <summary>
    ///   Indicates a <c>.dependency</c> file, in the plugin's directory or
    ///   next to a single-DLL plugin, says this version was installed as a
    ///   dependency of another plugin. The plugin is only installed as a
    ///   dependency while every installed version has one; see
    ///   <see cref="IPluginManager.IsInstalledAsDependency"/>.
    /// </summary>
    public bool IsDependency { get; internal set; }

    /// <summary>
    ///   Indicates the plugin has no enabled state of its own: it is installed
    ///   as a dependency, and is enabled while an enabled plugin depends on
    ///   it. Enabling or disabling it gives it a state of its own.
    /// </summary>
    public bool IsEnabledOnDemand { get; internal set; }

    /// <summary>
    ///   Indicates the plugin is currently loaded in the current session.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Plugin), nameof(PluginType))]
    public required bool IsActive { get; init; }

    /// <summary>
    ///   Indicates a restart would change whether the plugin is loaded: it is
    ///   enabled, not loaded and can load, or it is loaded but disabled or no
    ///   longer able to load, such as when a plugin it requires was disabled.
    ///   An enabled plugin that is not loaded and cannot load is not waiting
    ///   on a restart; see <see cref="CanLoad"/> and
    ///   <see cref="CannotLoadReason"/> instead.
    /// </summary>
    public bool RestartPending => IsActive != (IsEnabled && CanLoad);

    /// <summary>
    ///   Indicates the plugin can be loaded by the current runtime. Missing
    ///   assemblies, incompatible ABI versions or unsatisfied plugin
    ///   dependencies will prevent loading. Follows the dependencies as they
    ///   are enabled, disabled, installed and uninstalled, so a loaded plugin
    ///   can show it will not load after a restart.
    /// </summary>
    public bool CanLoad { get; internal set; }

    /// <summary>
    ///   Why the plugin cannot be loaded, when <see cref="CanLoad"/> is
    ///   <c>false</c> and the reason is known. Otherwise <c>null</c>.
    /// </summary>
    public string? CannotLoadReason { get; internal set; }

    /// <summary>
    ///   Indicates the plugin failed while the server was starting: while it
    ///   was loaded, registered, initialized, set up or made ready. The
    ///   server does not finish starting until the plugin is disabled,
    ///   replaced or uninstalled and the server restarted.
    /// </summary>
    [MemberNotNullWhen(true, nameof(FailureReason))]
    public bool HasFailed => FailureReason is not null;

    /// <summary>
    ///   What the plugin was doing when it failed and the error it failed
    ///   with, when <see cref="HasFailed"/> is <c>true</c>. Otherwise
    ///   <c>null</c>.
    /// </summary>
    public string? FailureReason { get; internal set; }

    /// <summary>
    ///   Indicates if the plugin can be uninstalled by the user.
    /// </summary>
    public required bool CanUninstall { get; init; }

    /// <summary>
    ///   The instance of the plugin, if it has been loaded.
    /// </summary>
    public required IPlugin? Plugin { get; init; }

    /// <summary>
    ///   The type of the plugin, if it has been loaded.
    /// </summary>
    public required Type? PluginType { get; init; }

    /// <summary>
    ///   The plugin's main configuration, when the plugin declares one through
    ///   <see cref="IPlugin{TConfiguration}"/> and it was registered.
    /// </summary>
    public ConfigurationInfo? ConfigurationInfo { get; internal set; }

    /// <summary>
    ///   The type used for the plugin to register its services with the core.
    /// </summary>
    public required Type? ServiceRegistrationType { get; init; }

    /// <summary>
    ///   The type used for the plugin to register it's application options with 
    ///   the core.
    /// </summary>
    public required Type? ApplicationRegistrationType { get; init; }

    /// <summary>
    ///   The directory containing the plugin DLLs, if the plugin is not placed
    ///   in the root of the plugins directory.
    /// </summary>
    public required string? ContainingDirectory { get; init; }

    /// <summary>
    ///   All DLLs for the plugin. The first path will always be the main DLL
    ///   which contains the plugin implementation.
    /// </summary>
    public required IReadOnlyList<string> DLLs { get; init; }

    /// <summary>
    ///   All loaded types for the plugin.
    /// </summary>
    public required IReadOnlyList<Type> Types { get; init; }

    /// <summary>
    ///   The dependencies of this installed plugin version.
    /// </summary>
    public required IReadOnlyList<PluginDependency> Dependencies { get; init; }

    /// <summary>
    ///   Get the pages exposed by the plugin.
    /// </summary>
    /// <returns>
    ///   The pages exposed by the plugin.
    /// </returns>
    public IReadOnlyList<LocalPluginPage> GetPages()
    {
        if (Plugin is null)
            return [];

        return Plugin.GetPages()
            .Select(x => new LocalPluginPage
            {
                ID = UuidUtility.GetV5(x.Url, ID),
                Name = x.Name,
                Url = x.Url,
                CanEmbed = x.CanEmbed,
                Visibility = x.Visibility,
                PluginInfo = this,
            })
            .DistinctBy(x => x.Name)
            .DistinctBy(x => x.Url)
            .ToList();
    }

    /// <summary>
    ///   Get the valid features advertised by the plugin, in the order the
    ///   plugin returned them.
    /// </summary>
    /// <returns>
    ///   The features advertised by the plugin.
    /// </returns>
    public IReadOnlyList<LocalPluginFeature> GetFeatures()
        => GetFeatures(null);

    /// <summary>
    ///   Get the valid features advertised by the plugin, in the order the
    ///   plugin returned them, reporting every dropped feature.
    /// </summary>
    /// <param name="onInvalidFeature">
    ///   Called with the dropped feature and the reason it was dropped.
    /// </param>
    /// <returns>
    ///   The features advertised by the plugin.
    /// </returns>
    internal IReadOnlyList<LocalPluginFeature> GetFeatures(Action<PluginFeature?, string>? onInvalidFeature)
    {
        if (Plugin is null)
            return [];

        var names = new HashSet<string>();
        var features = new List<LocalPluginFeature>();
        foreach (var feature in Plugin.GetFeatures())
        {
            if (!PluginFeature.IsValid(feature, out var error))
            {
                onInvalidFeature?.Invoke(feature, error!);
                continue;
            }

            if (!names.Add(feature!.Name))
            {
                onInvalidFeature?.Invoke(feature, "Another feature with the same name was already advertised.");
                continue;
            }

            features.Add(new()
            {
                Name = feature.Name,
                Version = new(feature.Version.Major, feature.Version.Minor, Math.Max(feature.Version.Build, 0)),
                Visibility = feature.Visibility,
                Metadata = feature.Metadata,
                PluginInfo = this,
            });
        }

        return features;
    }
}
