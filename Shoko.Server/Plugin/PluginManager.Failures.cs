using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Server;

#pragma warning disable CS0618
namespace Shoko.Server.Plugin;

/// <summary>
///   Plugins that fail to start: why, what they refer to that is missing,
///   and the failures remembered for the next start.
/// </summary>
public partial class PluginManager
{
    #region Failures

    /// <summary>
    ///   The plugins that failed while the server was starting.
    /// </summary>
    /// <returns>Every plugin with a <see cref="LocalPluginInfo.FailureReason"/>, in load order.</returns>
    internal IReadOnlyList<LocalPluginInfo> GetFailedPlugins()
        => _pluginTypes.Where(pluginInfo => pluginInfo.HasFailed).ToList();

    /// <summary>
    ///   Records that a plugin failed a start-up step. A plugin that is not
    ///   loaded yet is also kept from loading, so the steps after this one
    ///   leave it out.
    /// </summary>
    /// <param name="pluginInfo">The plugin that failed.</param>
    /// <param name="doing">What the plugin was doing, for the log and the reason.</param>
    /// <param name="exception">The error it failed with.</param>
    internal void MarkFailed(LocalPluginInfo pluginInfo, string doing, Exception exception)
    {
        logger.LogError(exception, "Plugin \"{Name}\" failed while {Doing}. ({Version})", pluginInfo.Name, doing, pluginInfo.Version);
        MarkFailed(pluginInfo, DescribeFailure(doing, exception));
    }

    /// <summary>
    ///   Records that a plugin failed, for a reason already worded. A plugin
    ///   that is not loaded yet is also kept from loading.
    /// </summary>
    /// <param name="pluginInfo">The plugin that failed.</param>
    /// <param name="reason">Why, as shown to the admin.</param>
    private static void MarkFailed(LocalPluginInfo pluginInfo, string reason)
    {
        // The first failure is the one to show; what follows is usually its fallout.
        pluginInfo.FailureReason ??= reason;
        if (pluginInfo.IsActive)
            return;

        pluginInfo.CanLoad = false;
        pluginInfo.CannotLoadReason ??= pluginInfo.FailureReason;
    }

    /// <summary>
    ///   Creates the exports of every loaded plugin for a start-up step. A
    ///   plugin's export that cannot be created fails the plugin, where one
    ///   asked for later is only skipped: nothing may quietly go missing from
    ///   a start.
    /// </summary>
    /// <typeparam name="T">The contract to create the exports of.</typeparam>
    /// <returns>The exports that could be created.</returns>
    private IEnumerable<T> CreateExports<T>()
    {
        foreach (var type in GetTypes<T>().ToList())
        {
            object instance;
            try
            {
                instance = ActivatorUtilities.GetServiceOrCreateInstance(ISystemService.StaticServices, type);
            }
            catch (Exception ex)
            {
                // The core's own are not a plugin's failure to isolate.
                if (GetPluginInfo(type.Assembly) is { } pluginInfo && pluginInfo.ID != CorePlugin.StaticID)
                    MarkFailed(pluginInfo, $"creating its {typeof(T).Name} \"{type.Name}\"", ex);
                else
                    logger.LogWarning(ex, "Unable to initialize instance of type {TypeName}", type.FullName);
                continue;
            }

            yield return (T)instance;
        }
    }

    /// <summary>
    ///   Finds the plugin whose code an error was thrown from, going by the
    ///   stack traces of the error and of those inside it.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <returns>The first plugin other than the core found in a stack trace, or <c>null</c>.</returns>
    internal LocalPluginInfo? FindPluginBehind(Exception exception)
    {
        var pending = new Queue<Exception>([exception]);
        while (pending.TryDequeue(out var current))
        {
            foreach (var frame in new StackTrace(current, fNeedFileInfo: false).GetFrames())
                if (frame.GetMethod()?.DeclaringType?.Assembly is { } assembly &&
                    GetPluginInfo(assembly) is { } pluginInfo && pluginInfo.ID != CorePlugin.StaticID)
                    return pluginInfo;

            if (current is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions)
                    pending.Enqueue(inner);
            else if (current.InnerException is { } inner)
                pending.Enqueue(inner);
        }

        return null;
    }

    private static string DescribeFailure(string doing, Exception exception)
    {
        // Reflection wraps what the plugin threw, and the wrapper says nothing.
        while (exception is TargetInvocationException { InnerException: { } inner })
            exception = inner;

        return $"The plugin failed while {doing}: {exception.GetType().Name}: {exception.Message}";
    }

    private const string HostMismatchMessage = "The plugin was built for a different version of the server and refers to what this server does not have";

    private const string RememberedFailuresFile = "failed-starts.json";

    private IReadOnlyDictionary<string, PluginReferenceChecker.AssemblySurface>? _hostSurfaces;

    private IReadOnlySet<string>? _hostProvidedAssemblies;

    /// <summary>
    ///   What the server's own assemblies define, read once.
    /// </summary>
    private IReadOnlyDictionary<string, PluginReferenceChecker.AssemblySurface> GetHostSurfaces()
        => _hostSurfaces ??= ReadSurfaces(new[] { typeof(IPlugin).Assembly, typeof(IQueueScheduler).Assembly, typeof(PluginManager).Assembly }
            .Select(assembly => assembly.Location)
            .Where(location => location.Length > 0));

    /// <summary>
    ///   Why a plugin's types could not be read: built for another version of
    ///   the server when the missing type should come from the server or the
    ///   runtime, naming it, or else a missing dependency.
    /// </summary>
    /// <param name="exception">What reading the plugin's types threw.</param>
    /// <returns>The reason, as shown to the admin.</returns>
    private string DescribeTypeLoadFailure(Exception exception)
    {
        const string AssemblyMarker = "from assembly '";
        if (exception is not TypeLoadException { TypeName: { Length: > 0 } typeName } typeLoad)
            return MissingDependenciesMessage;

        var start = typeLoad.Message.IndexOf(AssemblyMarker, StringComparison.Ordinal);
        var end = start < 0 ? -1 : typeLoad.Message.IndexOfAny([',', '\''], start + AssemblyMarker.Length);
        return end > 0 && GetHostProvidedAssemblies().Contains(typeLoad.Message[(start + AssemblyMarker.Length)..end])
            ? $"{HostMismatchMessage}: {typeName}."
            : MissingDependenciesMessage;
    }

    /// <summary>
    ///   The names of the assemblies every plugin gets from the server or the
    ///   runtime, whichever copy a plugin ships beside itself.
    /// </summary>
    private IReadOnlySet<string> GetHostProvidedAssemblies()
    {
        if (_hostProvidedAssemblies is not null)
            return _hostProvidedAssemblies;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
            foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                names.Add(Path.GetFileNameWithoutExtension(path));
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            names.Add(Path.GetFileNameWithoutExtension(path));
        return _hostProvidedAssemblies = names;
    }

    private Dictionary<string, PluginReferenceChecker.AssemblySurface> ReadSurfaces(IEnumerable<string> dlls)
    {
        var surfaces = new Dictionary<string, PluginReferenceChecker.AssemblySurface>(StringComparer.OrdinalIgnoreCase);
        foreach (var dll in dlls)
        {
            try
            {
                if (PluginReferenceChecker.AssemblySurface.Read(dll) is { } surface)
                    surfaces.TryAdd(surface.Name, surface);
            }
            // Native libraries and unreadable files are not this check's to judge.
            catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.LogTrace(ex, "Skipping a DLL whose metadata could not be read; {DllPath}", dll);
            }
        }

        return surfaces;
    }

    /// <summary>
    ///   Checks the plugin's DLLs for references to types and members the
    ///   given assemblies do not have.
    /// </summary>
    /// <param name="dlls">The plugin's DLLs.</param>
    /// <param name="targets">The assemblies to check against, by simple name.</param>
    /// <param name="message">What the mismatch is, to lead the reason with.</param>
    /// <returns>Why the plugin does not fit, or <c>null</c> when nothing is missing.</returns>
    private string? FindContractMismatch(IReadOnlyList<string> dlls, IReadOnlyDictionary<string, PluginReferenceChecker.AssemblySurface> targets, string message)
    {
        if (targets.Count is 0)
            return null;

        var missing = new List<string>();
        foreach (var dll in dlls)
        {
            if (targets.ContainsKey(Path.GetFileNameWithoutExtension(dll)))
                continue;

            try
            {
                missing.AddRange(PluginReferenceChecker.FindMissingReferences(dll, targets));
            }
            // Native libraries and unreadable files are not this check's to judge.
            catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.LogTrace(ex, "Skipping the contract check of a DLL that could not be read; {DllPath}", dll);
            }
        }

        if (missing.Count is 0)
            return null;

        const int Shown = 5;
        var distinct = missing.Distinct().ToList();
        var listed = string.Join(", ", distinct.Take(Shown));
        var rest = distinct.Count > Shown ? $" and {distinct.Count - Shown} more" : string.Empty;
        return $"{message}: {listed}{rest}. Update the plugin, or disable or uninstall it.";
    }

    /// <summary>
    ///   Fails every enabled plugin that refers to types or members a plugin
    ///   it depends on does not have, the way a plugin built for another
    ///   server is failed. Only the assemblies the dependency alone ships are
    ///   looked at: one the server, the runtime or the plugin itself also
    ///   has is not the dependency's to answer for.
    /// </summary>
    private void FailPluginsNotFittingTheirDependencies()
    {
        var enabled = _pluginTypes.Where(pluginInfo => pluginInfo.IsEnabled).ToList();
        foreach (var pluginInfo in enabled)
        {
            if (!pluginInfo.CanLoad || pluginInfo.ID == CorePlugin.StaticID)
                continue;

            var ownAssemblies = pluginInfo.DLLs.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in pluginInfo.Dependencies)
            {
                if (enabled.FirstOrDefault(other => other.ID == dependency.PluginID) is not { CanLoad: true } target || target.ID == CorePlugin.StaticID)
                    continue;

                var surfaces = ReadSurfaces(target.DLLs.Where(dll =>
                    Path.GetFileNameWithoutExtension(dll) is var name && !ownAssemblies.Contains(name) && !GetHostProvidedAssemblies().Contains(name)));
                var reason = FindContractMismatch(
                    pluginInfo.DLLs,
                    surfaces,
                    $"The plugin was built for a different version of plugin \"{target.Name}\" ({target.Version.Version}) and refers to what it does not have"
                );
                if (reason is null)
                    continue;

                logger.LogError("Not starting plugin \"{Name}\". {Reason} ({Version})", pluginInfo.Name, reason, pluginInfo.Version);
                MarkFailed(pluginInfo, reason);
                break;
            }
        }
    }

    /// <summary>
    ///   Checks the namespace stamped into a plugin against
    ///   <see cref="PluginNamespaceRules"/>.
    /// </summary>
    /// <param name="stamped">The stamped namespace, if any.</param>
    /// <returns>
    ///   The namespace when the plugin claims a valid one, and why the plugin
    ///   fails when it claims one that is not.
    /// </returns>
    internal static (string? Namespace, string? FailureReason) CheckNamespace(string? stamped)
    {
        if (string.IsNullOrEmpty(stamped))
            return (null, null);

        return PluginNamespaceRules.Validate(stamped) is { } error
            ? (null, $"The plugin claims the namespace \"{stamped}\", which is not valid: {error}. Update the plugin, or disable or uninstall it.")
            : (stamped, null);
    }

    /// <summary>
    ///   Fails every plugin the scan picked to load that claims the namespace
    ///   of another one, naming the others, so the admin keeps one of them.
    /// </summary>
    internal void FailPluginsClaimingTheSameNamespace()
    {
        var claims = _pluginTypes
            .Where(pluginInfo => pluginInfo is { IsEnabled: true, Namespace: not null } && pluginInfo.ID != CorePlugin.StaticID)
            .GroupBy(pluginInfo => pluginInfo.Namespace!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);
        foreach (var group in claims)
        {
            foreach (var pluginInfo in group)
            {
                var others = group.Where(other => other != pluginInfo).Select(other => $"\"{other.Name}\" ({other.ID})").ToList();
                var named = others.Count is 1
                    ? $"plugin {others[0]} also claims"
                    : $"plugins {string.Join(", ", others.Take(others.Count - 1))} and {others[^1]} also claim";
                var reason = $"The plugin claims the namespace \"{group.Key}\", which {named}. Disable, replace or uninstall all but one of them.";
                logger.LogError("Not starting plugin \"{Name}\". {Reason} ({Version})", pluginInfo.Name, reason, pluginInfo.Version);
                MarkFailed(pluginInfo, reason);
            }
        }
    }

    /// <summary>
    ///   Writes down that a plugin took the start down in a way that leaves
    ///   no web server to say so, so the next start leaves it out and says
    ///   why. Nothing is thrown: this runs while the start is already failing.
    /// </summary>
    /// <param name="pluginInfo">The plugin the failure was traced to.</param>
    /// <param name="doing">What the server was doing.</param>
    /// <param name="exception">The error.</param>
    internal void RememberFailureForNextStart(LocalPluginInfo pluginInfo, string doing, Exception exception)
    {
        try
        {
            while (exception is TargetInvocationException { InnerException: { } inner })
                exception = inner;

            var path = Path.Join(applicationPaths.PluginsPath, RememberedFailuresFile);
            var remembered = new Dictionary<string, string>
            {
                [GetRememberedFailureKey(pluginInfo.ID, pluginInfo.Version.Version)] =
                    $"The plugin stopped the server from starting the last time, while {doing}: {exception.GetType().Name}: {exception.Message.TrimEnd('.')}. It was left out of this start, and the next start tries it again.",
            };
            Directory.CreateDirectory(applicationPaths.PluginsPath);
            File.WriteAllText(path, JsonConvert.SerializeObject(remembered));
            logger.LogError(
                exception,
                "Plugin \"{Name}\" stopped the server from starting while {Doing}. The next start leaves it out and says so. ({Version})",
                pluginInfo.Name, doing, pluginInfo.Version
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write down the plugin that stopped the start");
        }
    }

    /// <summary>
    ///   Reads the plugins written down by <see cref="RememberFailureForNextStart"/>
    ///   and forgets them, so each is left out of one start only.
    /// </summary>
    /// <returns>The reason by plugin and version; empty when none was written down.</returns>
    private Dictionary<string, string> TakeRememberedFailures()
    {
        var path = Path.Join(applicationPaths.PluginsPath, RememberedFailuresFile);
        try
        {
            if (!File.Exists(path))
                return [];

            var remembered = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            File.Delete(path);
            return remembered;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the plugins that stopped the last start; {Path}", path);
            return [];
        }
    }

    private static string GetRememberedFailureKey(Guid pluginID, Version version)
        => $"{pluginID}@{version}";

    #endregion
}
