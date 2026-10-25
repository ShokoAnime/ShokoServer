using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin;

/// <summary>
///   Reading a plugin version from disk without registering anything, and
///   the version information stamped into its assembly.
/// </summary>
public partial class PluginManager
{
    #region Loading

    private IEnumerable<(string?, string[], bool)> GetPluginDirectories()
    {
        // Load plugins from the system directory.
        var systemPluginDir = Path.Join(applicationPaths.ApplicationPath, "plugins");
        if (Directory.Exists(systemPluginDir))
        {
            foreach (var filePath in Directory.GetFiles(systemPluginDir, "*.dll", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.ChangeExtension(filePath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin DLL file marked for removal: {Path}", filePath);
                    try
                    {
                        File.Delete(filePath);
                        File.Delete(removeFile);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin DLL file marked for removal: {Path}", filePath);
                    }
                    continue;
                }

                yield return (null, [filePath], true);
            }

            foreach (var directoryPath in Directory.GetDirectories(systemPluginDir, "*", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.Join(directoryPath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin directory marked for removal: {Path}", directoryPath);
                    try
                    {
                        Directory.Delete(directoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin directory marked for removal: {Path}", directoryPath);
                    }
                    continue;
                }

                yield return (directoryPath, Directory.GetFiles(directoryPath, "*.dll", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }), true);
            }
        }

        // Load plugins from the user config directory.
        var userPluginDir = applicationPaths.PluginsPath;
        if (Directory.Exists(userPluginDir))
        {
            foreach (var filePath in Directory.GetFiles(userPluginDir, "*.dll", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.ChangeExtension(filePath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin DLL file marked for removal: {Path}", filePath);
                    try
                    {
                        File.Delete(filePath);
                        File.Delete(removeFile);
                        File.Delete(Path.ChangeExtension(filePath, Keep));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin DLL file marked for removal: {Path}", filePath);
                    }
                    continue;
                }

                yield return (null, [filePath], false);
            }

            foreach (var directoryPath in Directory.GetDirectories(userPluginDir, "*", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                // Skip repositories metadata directory.
                if (Path.GetFileName(directoryPath) is PluginPackageManager.Repositories)
                    continue;

                var removeFile = Path.Join(directoryPath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin directory marked for removal: {Path}", directoryPath);
                    try
                    {
                        Directory.Delete(directoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin directory marked for removal: {Path}", directoryPath);
                    }
                    continue;
                }

                yield return (directoryPath, Directory.GetFiles(directoryPath, "*.dll", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }), false);
            }
        }
    }

    /// <inheritdoc/>
    IReadOnlyList<InstalledPlugin> IInstalledPluginReader.ReadInstalledPlugins()
    {
        // A copy, so plugins seen for the first time are not written to the settings before the scan.
        var settings = ISettingsProvider.Instance.GetSettings(copy: true);
        var settingsChanged = false;
        var infos = new List<(InternalPluginInfo Info, bool IsSystem)>();
        foreach (var (directoryPath, dlls, isSystem) in GetPluginDirectories())
        {
            if (LoadInternalPluginInfo(directoryPath, dlls, isSystem, settings, ref settingsChanged) is { } info)
                infos.Add((info, isSystem));
        }

        // As the scan does, so a plugin installed as a dependency counts as enabled while something needs it.
        ApplyDemand(infos.Select(tuple => tuple.Info).ToList(), settings.Plugins.EnabledPlugins);
        return infos
            .Select(tuple => new InstalledPlugin(
                tuple.Info.ID,
                tuple.Info.Name,
                tuple.Info.Version.Version,
                tuple.Info.IsEnabled,
                tuple.Info.IsPinned,
                tuple.IsSystem,
                tuple.Info.IsDependency,
                tuple.Info.Dependencies,
                tuple.Info.IsEnabledOnDemand
            ))
            .ToList();
    }

    /// <inheritdoc/>
    InstalledPlugin? IInstalledPluginReader.ReadPlugin(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return null;

        var settings = ISettingsProvider.Instance.GetSettings(copy: true);
        var settingsChanged = false;
        var dlls = Directory.GetFiles(directoryPath, "*.dll", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System });
        return LoadInternalPluginInfo(directoryPath, dlls, false, settings, ref settingsChanged) is { } info
            ? new(info.ID, info.Name, info.Version.Version, info.IsEnabled, info.IsPinned, IsDependency: info.IsDependency, Dependencies: info.Dependencies)
            : null;
    }

    private InternalPluginInfo? LoadInternalPluginInfo(string? containingDirectory, string[] dlls)
    {
        var settingsChanged = false;
        var settings = ISettingsProvider.Instance.GetSettings();
        var internalPluginInfo = LoadInternalPluginInfo(containingDirectory, dlls, false, settings, ref settingsChanged);
        if (settingsChanged)
            ISettingsProvider.Instance.SaveSettings();

        if (internalPluginInfo is not null)
            logger.LogInformation("Loaded inactive plugin \"{Name}\". ({DllName}, {Version})", internalPluginInfo.Name, Path.GetFileNameWithoutExtension(internalPluginInfo.DLLs[0]), internalPluginInfo.Version);
        return internalPluginInfo;
    }

    private const string LegacyNamespaceMessage = "This plugin uses the deprecated Shoko.Plugin.Abstractions namespace and is incompatible with this version of Shoko Server. Disable or uninstall it, or install a version built for Shoko.Abstractions.";

    private const string AbiTooNewMessage = "The plugin failed to load because it references a newer version of Shoko.Abstractions than what this server supports.";

    private const string AbiTooOldMessage = "This plugin was built for an older major version of Shoko.Abstractions and is incompatible with this version of Shoko Server. Disable or uninstall it, or install a version built for this server.";

    private const string MissingDependenciesMessage = "The plugin failed to load due to missing dependencies.";

    private const string InvalidDependenciesMessage = "The plugin failed to load because its embedded list of plugin dependencies is invalid.";

    /// <summary>
    ///   Picks the DLL of a plugin folder that resolves its own dependencies:
    ///   the first with a <c>.deps.json</c> that holds an <see cref="IPlugin"/>,
    ///   since a plugin's own libraries may ship a <c>.deps.json</c> too.
    /// </summary>
    /// <param name="dlls">The folder's DLLs.</param>
    /// <returns>
    ///   The DLL, the first with a <c>.deps.json</c> when none of them holds
    ///   an <see cref="IPlugin"/>, or <c>null</c> when none has a
    ///   <c>.deps.json</c>.
    /// </returns>
    private string? FindSelfResolvingPlugin(string[] dlls)
    {
        var candidates = dlls.Where(dll => Path.Exists(Path.ChangeExtension(dll, ".deps.json"))).ToList();
        if (candidates.Count <= 1)
            return candidates.FirstOrDefault();

        foreach (var candidate in candidates)
        {
            if (HoldsPlugin(candidate))
                return candidate;

            logger.LogDebug("Passing over a DLL with a .deps.json because it holds no IPlugin; {DllPath}", candidate);
        }

        return candidates[0];
    }

    /// <summary>
    ///   Whether a DLL defines a type implementing <see cref="IPlugin"/>, or
    ///   the legacy namespace's <c>IPlugin</c>, read in a load context of its
    ///   own that is unloaded again.
    /// </summary>
    /// <param name="dllPath">The DLL, with its <c>.deps.json</c> beside it.</param>
    /// <returns><c>true</c> when one of the types it could load implements it.</returns>
    private static bool HoldsPlugin(string dllPath)
    {
        var alc = new IsolatedLoadContext(dllPath);
        try
        {
            Type?[] types;
            try
            {
                types = alc.LoadFromAssemblyPath(dllPath).GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            return types.Any(type =>
            {
                try
                {
                    return type is not null && type.GetInterfaces().Any(interfaceType => interfaceType == typeof(IPlugin) || interfaceType.FullName is "Shoko.Plugin.Abstractions.IPlugin");
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            alc.Unload();
        }
    }

    private InternalPluginInfo? LoadInternalPluginInfo(string? dirPath, string[] dlls, bool isSystem, IServerSettings settings, ref bool settingsChanged)
    {
        var selfResolvingPluginPath = dirPath is not null ? FindSelfResolvingPlugin(dlls) : dlls.FirstOrDefault(dll => Path.Exists(Path.ChangeExtension(dll, ".deps.json")));
        var dllsToLoad = dirPath is not null && selfResolvingPluginPath is not null ? [selfResolvingPluginPath] : dlls;
        var alc = new IsolatedLoadContext(selfResolvingPluginPath);
        try
        {
            foreach (var dllPath in dllsToLoad)
            {
                var name = Path.GetFileNameWithoutExtension(dllPath);
                try
                {
                    var assembly = alc.LoadFromAssemblyPath(dllPath);
                    var assemblyName = assembly.GetName().Name;
                    if (string.IsNullOrEmpty(assemblyName) || !string.Equals(assemblyName, name, StringComparison.Ordinal))
                    {
                        logger.LogInformation("Skipping DLL because the loaded assembly does not have the same name as the file; {DllPath}", dllPath);
                        continue;
                    }

                    var version = ReadVersionInformationFromAssembly(assembly, out var isLegacyNamespace, out var metadataAttributeDict);
                    if (version is null)
                    {
                        // A DLL that references the abstractions is meant to be a plugin, so say why it is left out.
                        if (assembly.GetReferencedAssemblies().Any(reference => reference.Name is "Shoko.Abstractions" or "Shoko.Plugin.Abstractions"))
                            logger.LogWarning("Skipping plugin DLL because its assembly version is 0.0.0 or missing; {DllPath}", dllPath);
                        continue;
                    }

                    var authors = assembly.GetCustomAttribute<AssemblyCompanyAttribute>() is { Company: { Length: > 0 } companyName }
                        ? companyName
                        : null;
                    var repositoryUrl = metadataAttributeDict.ContainsKey(RepositoryUrl) && metadataAttributeDict[RepositoryUrl] is { Length: > 0 } ? metadataAttributeDict[RepositoryUrl] : null;
                    var homepageUrl = metadataAttributeDict.ContainsKey(PackageProjectUrl) && metadataAttributeDict[PackageProjectUrl] is { Length: > 0 } ? metadataAttributeDict[PackageProjectUrl] : null;
                    var tags = metadataAttributeDict.ContainsKey(PackageTags) &&
                        metadataAttributeDict[PackageTags]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } packageTags
                        ? packageTags.Select(tag => tag.ToLowerInvariant()).Distinct().ToArray()
                        : [];

                    // A version installed as a dependency gets no enabled state of its own, so it can follow
                    // its dependents. Otherwise TryAdd, because if it made it this far, then it's missing or true.
                    var isDependency = File.Exists(GetDependencyFile(dirPath, dllPath));
                    if (!isDependency && settings.Plugins.EnabledPlugins.TryAdd(name, true))
                        settingsChanged = true;

                    if (isLegacyNamespace)
                    {
                        logger.LogWarning("Found plugin using deprecated Shoko.Plugin.Abstractions namespace. This plugin is incompatible and needs to be updated. ({DllName}, {Version})", name, version);

                        // Legacy plugins won't have embedded metadata, so
                        // generate a deterministic stub ID instead.
                        return new()
                        {
                            // Create an unique ID for this specific version that failed to load.
                            ID = UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = name,
                            Description = isLegacyNamespace
                                ? LegacyNamespaceMessage
                                : MissingDependenciesMessage,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = GetInstalledAt(dllPath),
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsKept = File.Exists(GetKeepFile(dirPath, dllPath)),
                            IsDependency = isDependency,
                            IsEnabled = settings.Plugins.EnabledPlugins.GetValueOrDefault(name),
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = LegacyNamespaceMessage,
                            IsLegacy = true,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    // Read embedded identity if possible for the paths below.
                    // Legacy plugins (checked above) won't have these.
                    var embeddedId = metadataAttributeDict.TryGetValue(PackageID, out var pid) && Guid.TryParse(pid, out var parsedId)
                        ? parsedId
                        : (Guid?)null;
                    var embeddedName = metadataAttributeDict.TryGetValue(PackageName, out var pn) ? pn : null;
                    var embeddedDescription = metadataAttributeDict.TryGetValue(PackageOverview, out var pd) ? pd : null;
                    if (embeddedId is null)
                    {
                        logger.LogWarning(
                            "Plugin does not have embedded identity metadata. ({DllName}, {Version}) " +
                            "Install Shoko.BuildTools (`dotnet tool install --global Shoko.BuildTools`) " +
                            "and rebuild with `shoko-build`, or add the `Shoko.BuildTools.Targets` " +
                            "NuGet package to your project for automatic metadata injection.",
                            name, version);
                    }

                    // A newer contract waits for the server to be upgraded; an older major never loads here.
                    var abiMismatch = version.AbstractionVersion > AbstractionVersion
                        ? AbiTooNewMessage
                        : version.AbstractionVersion.Major < AbstractionVersion.Major
                            ? AbiTooOldMessage
                            : null;
                    if (abiMismatch is not null)
                    {
                        logger.LogInformation("Skipping DLL. {Reason} ({DllName}, {Version})", abiMismatch, name, version);
                        return new()
                        {
                            // Create an unique ID for this specific version if it failed to load.
                            ID = embeddedId ?? UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = embeddedName ?? name,
                            Description = embeddedDescription is not null
                                ? $"{abiMismatch}\n\n{embeddedDescription}"
                                : abiMismatch,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = GetInstalledAt(dllPath),
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsKept = File.Exists(GetKeepFile(dirPath, dllPath)),
                            IsDependency = isDependency,
                            IsEnabled = settings.Plugins.EnabledPlugins.GetValueOrDefault(name),
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = abiMismatch,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    // A dependency we cannot read is one we cannot enforce, so
                    // the plugin does not load at all.
                    var embeddedDependencies = PluginDependencyList.Parse(
                        metadataAttributeDict.TryGetValue(PackageDependencies, out var depsRaw) ? depsRaw : null,
                        out var dependencyErrors);
                    dependencyErrors.AddRange(PluginDependencyList.Validate(embeddedDependencies));
                    if (dependencyErrors.Count > 0)
                    {
                        logger.LogError(
                            "Skipping DLL because its embedded plugin dependencies are invalid: {Errors}; {DllPath}",
                            string.Join(" ", dependencyErrors),
                            dllPath);
                        return new()
                        {
                            // Create an unique ID for this specific version if it failed to load.
                            ID = embeddedId ?? UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = embeddedName ?? name,
                            Description = embeddedDescription is not null
                                ? $"{InvalidDependenciesMessage}\n\n{embeddedDescription}"
                                : InvalidDependenciesMessage,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = GetInstalledAt(dllPath),
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsKept = File.Exists(GetKeepFile(dirPath, dllPath)),
                            IsDependency = isDependency,
                            IsEnabled = settings.Plugins.EnabledPlugins.GetValueOrDefault(name),
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = InvalidDependenciesMessage,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    Type[] pluginTypes;
                    try
                    {
                        pluginTypes = assembly.GetExportedTypes();
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to get exported types from DLL. Ensure all dependencies are available; {DllPath}", dllPath);

                        var typeLoadReason = DescribeTypeLoadFailure(e);
                        return new()
                        {
                            // Create an unique ID for this specific version if it failed to load.
                            ID = embeddedId ?? UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = embeddedName ?? name,
                            Description = embeddedDescription is not null
                                ? $"{typeLoadReason}\n\n{embeddedDescription}"
                                : typeLoadReason,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = GetInstalledAt(dllPath),
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsKept = File.Exists(GetKeepFile(dirPath, dllPath)),
                            IsDependency = isDependency,
                            IsEnabled = settings.Plugins.EnabledPlugins.GetValueOrDefault(name),
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = typeLoadReason,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    var pluginImpl = pluginTypes
                        .Where(a => a.GetInterfaces().Contains(typeof(IPlugin)))
                        .ToList();
                    if (pluginImpl.Count == 0)
                        continue;

                    if (pluginImpl.Count > 1)
                    {
                        logger.LogWarning(
                            "Multiple implementations of IPlugin found in {DllName}. Using the first implementation: {ImplName}",
                            name,
                            pluginImpl[0].Name
                        );
                    }

                    var registrationImpl = pluginTypes
                        .Where(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration)))
                        .ToList();
                    if (registrationImpl.Count > 1)
                    {
                        logger.LogWarning(
                            "Multiple IPluginServiceRegistrations found in {DllName}. Using the first implementation: {ImplName}",
                            name,
                            registrationImpl[0].Name
                        );
                    }

                    if (registrationImpl.Count > 0)
                        logger.LogInformation("Found plugin with services. ({DllName}, {Version})", name, version);
                    else
                        logger.LogInformation("Found plugin. ({DllName}, {Version})", name, version);

                    if (!settings.Plugins.Priority.Contains(name))
                    {
                        settings.Plugins.Priority.Add(name);
                        settingsChanged = true;
                    }

                    IPlugin instance;
                    try
                    {
                        instance = (IPlugin)Activator.CreateInstance(pluginImpl[0])!;
                    }
                    catch (Exception e)
                    {
                        // Kept and failed, where skipping the DLL would make the plugin vanish without a word.
                        logger.LogError(e, "Plugin failed while being created to read its identity. ({DllName}, {Version})", name, version);
                        return new()
                        {
                            // Create an unique ID for this specific version if it failed to load.
                            ID = embeddedId ?? UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = embeddedName ?? name,
                            Description = embeddedDescription?.CleanDescription() ?? string.Empty,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = GetInstalledAt(dllPath),
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsKept = File.Exists(GetKeepFile(dirPath, dllPath)),
                            IsDependency = isDependency,
                            IsEnabled = settings.Plugins.EnabledPlugins.GetValueOrDefault(name),
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.IndexOf(name),
                            CanLoad = true,
                            FailureReason = DescribeFailure("being created", e),
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    if (embeddedId.HasValue)
                    {
                        if (embeddedId.Value == CorePlugin.StaticID)
                        {
                            logger.LogWarning("Skipping {DllName} because it has the same ID as the core plugin.", dllPath);
                            continue;
                        }
                        if (embeddedId.Value != instance.ID)
                        {
                            logger.LogWarning("Skipping {DllName} because it has a different ID from the embedded metadata.", dllPath);
                            continue;
                        }
                    }
                    else if (instance.ID == CorePlugin.StaticID)
                    {
                        logger.LogWarning("Skipping {DllName} because it has the same ID as the core plugin.", dllPath);
                        continue;
                    }
                    // The names the build stamped, from the project's PluginThumbnail and PluginIcon, win over the plugin's own.
                    var thumbnailResourceName = metadataAttributeDict.GetValueOrDefault(PackageThumbnail) is { Length: > 0 } stampedThumbnail
                        ? stampedThumbnail
                        : instance.EmbeddedThumbnailResourceName;
                    var iconResourceName = metadataAttributeDict.GetValueOrDefault(PackageIcon) is { Length: > 0 } stampedIcon
                        ? stampedIcon
                        : instance.EmbeddedIconResourceName;
                    var thumbnailImage = PackageImageLoader.ReadEmbedded(assembly, assemblyName, thumbnailResourceName, ThumbnailKind, dllPath, logger);
                    var iconImage = PackageImageLoader.ReadEmbedded(assembly, assemblyName, iconResourceName, IconKind, dllPath, logger);

                    return new()
                    {
                        ID = embeddedId ?? instance.ID,
                        DllName = name,
                        Name = embeddedName ?? instance.Name,
                        Description = (embeddedDescription ?? instance.Description)?.CleanDescription() ?? string.Empty,
                        Version = version,
                        Authors = authors,
                        RepositoryUrl = repositoryUrl,
                        HomepageUrl = homepageUrl,
                        Tags = tags,
                        InstalledAt = GetInstalledAt(dllPath),
                        IsPinned = string.IsNullOrEmpty(dirPath)
                            ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                            : File.Exists(Path.Join(dirPath, Pinned)),
                        IsKept = File.Exists(GetKeepFile(dirPath, dllPath)),
                        IsDependency = isDependency,
                        IsEnabled = settings.Plugins.EnabledPlugins.GetValueOrDefault(name),
                        ContainingDirectory = dirPath,
                        Priority = settings.Plugins.Priority.IndexOf(name),
                        CanLoad = version.RuntimeIdentifier is IPluginManager.AnyRuntimeIdentifier || version.RuntimeIdentifier == RuntimeIdentifier,
                        CannotLoadReason = version.RuntimeIdentifier is IPluginManager.AnyRuntimeIdentifier || version.RuntimeIdentifier == RuntimeIdentifier
                            ? null
                            : $"The plugin is built for the \"{version.RuntimeIdentifier}\" runtime, not this server's \"{RuntimeIdentifier}\".",
                        CanUninstall = !isSystem,
                        DLLs = [dllPath, .. dlls.Except([dllPath])],
                        Thumbnail = thumbnailImage,
                        Icon = iconImage,
                        Dependencies = embeddedDependencies
                            .Select(dependency => new PluginDependency
                            {
                                PluginID = dependency.PluginID,
                                VersionRange = dependency.VersionRange,
                                IsOptional = dependency.IsOptional,
                            })
                            .ToList(),
                    };
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to check assembly {Name} for valid IPlugin and IPluginServiceRegistration implementations; {DllPath}", name, dllPath);
                    break;
                }
            }
        }
        finally
        {
            alc.Unload();
        }

        return null;
    }

    #endregion

    #region Version Information

    private const string RepositoryUrl = "RepositoryUrl";

    private const string PackageID = "PackageID";

    private const string PackageName = "PackageName";

    private const string PackageOverview = "PackageOverview";

    private const string PackageDependencies = "PackageDependencies";

    private const string PackageProjectUrl = "PackageProjectUrl";

    private const string PackageTags = "PackageTags";

    private const string PackageThumbnail = "PackageThumbnail";

    private const string PackageIcon = "PackageIcon";

    private const string NewRuntimeIdentifier = "RuntimeIdentifier";

    private const string NewReleaseTag = "ReleaseTag";

    private const string NewSourceRevision = "SourceRevision";

    private const string NewReleaseDate = "ReleaseDate";

    private const string NewReleaseChannel = "ReleaseChannel";

    private const string LegacyRuntimeIdentifier = "runtime";

    private const string LegacyReleaseTag = "tag";

    private const string LegacySourceRevision = "commit";

    private const string LegacyReleaseChannel = "channel";

    public const string LegacyReleaseDate = "date";

    private static VersionInformation? _serverVersionInformation;

    internal static VersionInformation GetVersionInformation()
        => _serverVersionInformation ??= ReadVersionInformationFromAssembly(Assembly.GetExecutingAssembly(), out _, out _)!;

    internal static VersionInformation? GetVersionInformation(Assembly assembly)
        => ReadVersionInformationFromAssembly(assembly, out _, out _)!;

    private static VersionInformation? ReadVersionInformationFromAssembly(Assembly assembly, out bool isLegacyNamespace, out Dictionary<string, string?> metadataAttributeDict)
    {
        var dllPath = assembly.Location!;
        var referencedAssemblies = assembly.GetReferencedAssemblies();
        var legacyRef = referencedAssemblies.FirstOrDefault(r => r.Name is "Shoko.Plugin.Abstractions");
        var newRef = referencedAssemblies.FirstOrDefault(r => r.Name is "Shoko.Abstractions");
        isLegacyNamespace = legacyRef is not null && newRef is null;
        metadataAttributeDict = [];
        var abstractionVersion = (isLegacyNamespace ? legacyRef : newRef)?.Version is { Major: var abiMajor, Minor: var abiMinor, Build: var abiBuild }
            ? new Version(abiMajor, abiMinor, abiBuild)
            : new(0, 0, 0);
        // Silently skip DLLs which doesn't reference the abstraction.
        if (abstractionVersion <= _invalidVersion)
            return null;

        var version = assembly.GetName().Version is { Major: var assMajor, Minor: var assMinor, Build: var assBuild, Revision: var assRevision }
            ? assRevision is <= 0 ? new(assMajor, assMinor, assBuild) : new(assMajor, assMinor, assBuild, assRevision)
            : new Version(0, 0, 0, 0);
        if (version <= _invalidVersion)
            return null;

        metadataAttributeDict = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(a => KeyValuePair.Create(a.Key, a.Value))
            .DistinctBy(kp => kp.Key)
            .ToDictionary();
        var extraVersionDict = GetApplicationExtraVersion(assembly);

        string runtime;
        if (metadataAttributeDict.ContainsKey(NewRuntimeIdentifier) && !string.IsNullOrEmpty(metadataAttributeDict[NewRuntimeIdentifier]))
            runtime = metadataAttributeDict[NewRuntimeIdentifier]!;
        else if (extraVersionDict.ContainsKey(NewRuntimeIdentifier) && !string.IsNullOrEmpty(extraVersionDict[NewRuntimeIdentifier]))
            runtime = extraVersionDict[NewRuntimeIdentifier];
        else if (extraVersionDict.ContainsKey(LegacyRuntimeIdentifier) && !string.IsNullOrEmpty(extraVersionDict[LegacyRuntimeIdentifier]))
            runtime = extraVersionDict[LegacyRuntimeIdentifier];
        else
            runtime = "any";

        var tag = (string?)null;
        if (metadataAttributeDict.ContainsKey(NewReleaseTag) && !string.IsNullOrEmpty(metadataAttributeDict[NewReleaseTag]))
            tag = metadataAttributeDict[NewReleaseTag];
        else if (extraVersionDict.ContainsKey(NewReleaseTag) && !string.IsNullOrEmpty(extraVersionDict[NewReleaseTag]))
            tag = extraVersionDict[NewReleaseTag];
        else if (extraVersionDict.ContainsKey(LegacyReleaseTag) && !string.IsNullOrEmpty(extraVersionDict[LegacyReleaseTag]))
            tag = extraVersionDict[LegacyReleaseTag];

        var sourceRevision = (string?)null;
        if (metadataAttributeDict.ContainsKey(NewSourceRevision) && !string.IsNullOrEmpty(metadataAttributeDict[NewSourceRevision]))
            sourceRevision = metadataAttributeDict[NewSourceRevision];
        else if (extraVersionDict.ContainsKey(NewSourceRevision) && !string.IsNullOrEmpty(extraVersionDict[NewSourceRevision]))
            sourceRevision = extraVersionDict[NewSourceRevision];
        else if (extraVersionDict.ContainsKey(LegacySourceRevision) && !string.IsNullOrEmpty(extraVersionDict[LegacySourceRevision]))
            sourceRevision = extraVersionDict[LegacySourceRevision];

        DateTime releasedAt;
        if (metadataAttributeDict.ContainsKey(NewReleaseDate) && DateTime.TryParse(metadataAttributeDict[NewReleaseDate], out var metaReleasedAt))
            releasedAt = metaReleasedAt.ToUniversalTime();
        else if (extraVersionDict.ContainsKey(NewReleaseDate) && DateTime.TryParse(extraVersionDict[NewReleaseDate], out var extraReleasedAt0))
            releasedAt = extraReleasedAt0.ToUniversalTime();
        else if (extraVersionDict.ContainsKey(LegacyReleaseDate) && DateTime.TryParse(extraVersionDict[LegacyReleaseDate], out var extraReleasedAt1))
            releasedAt = extraReleasedAt1.ToUniversalTime();
        else
            releasedAt = File.GetCreationTimeUtc(dllPath);

        var isDebug = assembly.GetCustomAttribute<DebuggableAttribute>() is { DebuggingFlags: > DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints };
        var releaseChannel = isDebug ? ReleaseChannel.Debug : version.Revision > 0 ? ReleaseChannel.Dev : ReleaseChannel.Stable;
        if (!isDebug)
        {
            if (metadataAttributeDict.ContainsKey(NewReleaseChannel) && !string.IsNullOrEmpty(metadataAttributeDict[NewReleaseChannel]) && Enum.TryParse<ReleaseChannel>(metadataAttributeDict[NewReleaseChannel]!, true, out var metaReleaseChannel))
                releaseChannel = metaReleaseChannel;
            else if (extraVersionDict.ContainsKey(NewReleaseChannel) && !string.IsNullOrEmpty(extraVersionDict[NewReleaseChannel]) && Enum.TryParse<ReleaseChannel>(extraVersionDict[NewReleaseChannel]!, true, out var extraReleaseChannel0))
                releaseChannel = extraReleaseChannel0;
            else if (extraVersionDict.ContainsKey(LegacyReleaseChannel) && !string.IsNullOrEmpty(extraVersionDict[LegacyReleaseChannel]) && Enum.TryParse<ReleaseChannel>(extraVersionDict[LegacyReleaseChannel]!, true, out var extraReleaseChannel1))
                releaseChannel = extraReleaseChannel1;
        }

        return new()
        {
            Version = version,
            RuntimeIdentifier = runtime,
            AbstractionVersion = abstractionVersion,
            SourceRevision = sourceRevision,
            ReleaseTag = tag,
            Channel = releaseChannel,
            ReleasedAt = releasedAt,
        };
    }

    private static Dictionary<string, string> GetApplicationExtraVersion(Assembly assembly)
    {
        if (assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) is not AssemblyInformationalVersionAttribute version)
            return [];

        return version.InformationalVersion.Split(",")
                .Select(raw => raw.Split("="))
                .Where(pair => pair.Length == 2 && !string.IsNullOrEmpty(pair[1]))
                .ToDictionary(pair => pair[0], pair => pair[1]);
    }

    #endregion
}
