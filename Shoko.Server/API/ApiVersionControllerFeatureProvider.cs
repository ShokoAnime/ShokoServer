using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Shoko.Server.API.v0.Controllers;
using Shoko.Server.API.v1.Implementations;
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

        // APIv1 Stream endpoints are used by both APIv1 and APIv2 clients, e.g. Shokodi, Nakamori, etc.
        if (typeInfo == typeof(ShokoServiceImplementationStream) && (webSettings.EnableAPIv1 || webSettings.EnableAPIv2))
            return true;

        // APIv0 Auth endpoints are used by both APIv1 and APIv2 clients.
        if (typeInfo == typeof(AuthenticationController) && (webSettings.EnableAPIv1 || webSettings.EnableAPIv2))
            return true;

        var ns = typeInfo.Namespace;
        if (ns?.Contains(".API.v1.") == true && !webSettings.EnableAPIv1)
            return false;
        if (ns?.Contains(".API.v2.") == true && !webSettings.EnableAPIv2)
            return false;
        if (ns?.Contains(".API.v3.") == true && !webSettings.EnableAPIv3)
            return false;
        if (typeInfo == typeof(IndexRedirectController) && !webSettings.EnableIndexRedirect)
            return false;
        if (typeInfo == typeof(PlexWebhook) && !webSettings.EnableLegacyPlexAPI)
            return false;
        return true;
    }
}
