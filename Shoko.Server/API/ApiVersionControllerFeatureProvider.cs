using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Shoko.Server.Settings;

namespace Shoko.Server.API;

/// <summary>
/// A custom controller feature provider that filters out API version controllers
/// based on the <see cref="WebSettings"/> configuration.
/// </summary>
/// <remarks>
/// This provider only affects controllers in the core assembly.
/// Plugin controllers from other assemblies are never filtered.
/// </remarks>
public class ApiVersionControllerFeatureProvider(WebSettings webSettings) : ControllerFeatureProvider
{
    private static readonly Assembly _serverAssembly = typeof(ApiVersionControllerFeatureProvider).Assembly;

    protected override bool IsController(TypeInfo typeInfo)
    {
        if (!base.IsController(typeInfo))
            return false;

        // Only filter controllers from the core assembly.
        if (typeInfo.Assembly != _serverAssembly)
            return true;

        var ns = typeInfo.Namespace;
        if (ns?.Contains(".API.v1.") == true && !webSettings.EnableAPIv1)
            return false;
        if (ns?.Contains(".API.v2.") == true && !webSettings.EnableAPIv2)
            return false;
        if (ns?.Contains(".API.v3.") == true && !webSettings.EnableAPIv3)
            return false;
        return true;
    }
}
