using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;

namespace Shoko.Server.API.Swagger;

/// <summary>
/// Document inclusion predicate that separates server and plugin controllers
/// into distinct Swagger documents per API version.
/// </summary>
/// <remarks>
/// Document naming convention:
/// - Server APIs: <c>v1</c>, <c>v2</c>, <c>v3</c>
/// - Plugin APIs: <c>{DllName}-v1</c>, <c>{DllName}-v2</c>, <c>{DllName}-v3</c>
/// Endpoints a plugin maps itself, rather than through a controller, only ever
/// go in that plugin's documents, and the unversioned ones in its <c>v1</c> one.
/// </remarks>
public class PluginDocumentInclusionPredicate
{
    /// <summary>
    /// The version group an unversioned mapped endpoint is listed under, the
    /// same one an unversioned controller gets from the default API version.
    /// </summary>
    public const string DefaultVersionGroup = "v1";

    /// <summary>
    /// The format of a version group's name, e.g. <c>v3</c> or <c>v2.1</c>.
    /// </summary>
    public const string VersionGroupFormat = "'v'VVV";

    private static readonly Assembly _serverAssembly = typeof(PluginDocumentInclusionPredicate).Assembly;

    private readonly IPluginManager _pluginManager;

    public PluginDocumentInclusionPredicate(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    /// <summary>
    /// Determines whether an API description should be included in the given Swagger document.
    /// </summary>
    /// <param name="documentName">The name of the Swagger document (e.g., "v3", "ReleaseExporter-v3").</param>
    /// <param name="apiDesc">The API description to evaluate.</param>
    /// <returns>True if the API should be included in the document.</returns>
    public bool Include(string documentName, ApiDescription apiDesc)
    {
        var controllerType = GetControllerType(apiDesc);
        if (controllerType is null)
            return IncludeEndpoint(documentName, apiDesc);

        var controllerAssembly = controllerType.Assembly;

        // Get the API version group name from the API description (e.g., "v0", "v1", "v2", "v3")
        // Default to "v1" when no version is explicitly set on the controller
        var groupName = string.IsNullOrEmpty(apiDesc.GroupName) ? DefaultVersionGroup : apiDesc.GroupName;

        // Server documents: "v0", "v1", "v2", "v3"
        if (documentName == groupName)
        {
            return controllerAssembly == _serverAssembly;
        }

        // Plugin documents: "{DllName}-v0", "{DllName}-v1", etc.
        if (documentName.EndsWith($"-{groupName}"))
        {
            var pluginDllName = documentName.Substring(0, documentName.Length - groupName.Length - 1);

            // Controller must be from a plugin assembly (not the server assembly)
            if (controllerAssembly == _serverAssembly)
                return false;

            // Find the plugin that owns this assembly
            var pluginInfo = _pluginManager.GetPluginInfos()
                .FirstOrDefault(p => p.IsEnabled && p.PluginType?.Assembly == controllerAssembly);

            if (pluginInfo is null)
                return false;

            // Match by DLL name (without extension)
            var expectedDllName = Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]);
            return pluginDllName.Equals(expectedDllName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Finds the plugin a mapped endpoint belongs to: the one its group name
    /// names, or else the one owning the assembly its handler is declared in.
    /// </summary>
    /// <param name="apiDesc">The API description of a mapped endpoint.</param>
    /// <returns>
    /// The enabled plugin owning the endpoint, or <c>null</c> when
    /// it belongs to the server or to no plugin.
    /// </returns>
    public LocalPluginInfo? GetEndpointOwner(ApiDescription apiDesc)
    {
        var metadata = apiDesc.ActionDescriptor.EndpointMetadata;
        if (metadata?.OfType<IEndpointGroupNameMetadata>().LastOrDefault()?.EndpointGroupName is { Length: > 0 } groupName &&
            _pluginManager.GetPluginInfos().FirstOrDefault(p => p.IsEnabled && IsNamed(p, groupName)) is { } namedPlugin)
            return IsServer(namedPlugin) ? null : namedPlugin;

        if (metadata?.OfType<MethodInfo>().FirstOrDefault()?.DeclaringType?.Assembly is not { } assembly || assembly == _serverAssembly)
            return null;

        var pluginInfo = _pluginManager is PluginManager pluginManager
            ? pluginManager.GetOwningPluginInfo(assembly)
            : _pluginManager.GetPluginInfos().FirstOrDefault(p => p.PluginType?.Assembly == assembly);
        return pluginInfo is { IsEnabled: true } && !IsServer(pluginInfo) ? pluginInfo : null;
    }

    private bool IncludeEndpoint(string documentName, ApiDescription apiDesc)
    {
        if (GetEndpointOwner(apiDesc) is not { } pluginInfo)
            return false;

        // A versioned endpoint is listed under its own version, an unversioned one under the default.
        var versionGroup = apiDesc.ApiVersion is { } apiVersion
            ? apiVersion.ToString(VersionGroupFormat, CultureInfo.InvariantCulture)
            : DefaultVersionGroup;
        var expectedName = $"{Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0])}-{versionGroup}";
        return documentName.Equals(expectedName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNamed(LocalPluginInfo pluginInfo, string name)
        => name.Equals(Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]), StringComparison.OrdinalIgnoreCase) ||
            name.Equals(pluginInfo.PluginType?.Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase);

    private static bool IsServer(LocalPluginInfo pluginInfo)
        => pluginInfo.PluginType?.Assembly == _serverAssembly;

    private static Type? GetControllerType(ApiDescription apiDesc)
    {
        if (apiDesc.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            return controllerAction.ControllerTypeInfo?.AsType();
        }

        return null;
    }
}
