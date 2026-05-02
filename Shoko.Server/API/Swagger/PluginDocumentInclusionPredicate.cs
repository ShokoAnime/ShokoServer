using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Shoko.Abstractions.Plugin;

#nullable enable
namespace Shoko.Server.API.Swagger;

/// <summary>
/// Document inclusion predicate that separates server and plugin controllers
/// into distinct Swagger documents per API version.
/// </summary>
/// <remarks>
/// Document naming convention:
/// - Server APIs: <c>v1</c>, <c>v2</c>, <c>v3</c>
/// - Plugin APIs: <c>{DllName}-v1</c>, <c>{DllName}-v2</c>, <c>{DllName}-v3</c>
/// </remarks>
public class PluginDocumentInclusionPredicate
{
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
            return false;

        var controllerAssembly = controllerType.Assembly;

        // Get the API version group name from the API description (e.g., "v0", "v1", "v2", "v3")
        // Default to "v1" when no version is explicitly set on the controller
        var groupName = string.IsNullOrEmpty(apiDesc.GroupName) ? "v1" : apiDesc.GroupName;

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
            var expectedDllName = System.IO.Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]);
            return pluginDllName.Equals(expectedDllName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static Type? GetControllerType(ApiDescription apiDesc)
    {
        if (apiDesc.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            return controllerAction.ControllerTypeInfo?.AsType();
        }

        return null;
    }
}
