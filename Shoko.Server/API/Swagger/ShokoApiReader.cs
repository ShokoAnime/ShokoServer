using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Versioning;

namespace Shoko.Server.API.Swagger;

public class ShokoApiReader : IApiVersionReader
{
    public void AddParameters(IApiVersionParameterDescriptionContext context)
    {
        context.AddParameter(null, ApiVersionParameterLocation.Path);
    }

    public string Read(HttpRequest request)
    {
        if (!string.IsNullOrEmpty(request.Headers["api-version"]))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(request.Query["api-version"]))
        {
            return null;
        }

        PathString[] apiv1 = { "/v1", "/api/Image", "/api/Kodi", "/api/Metro", "/api/Plex", "/Stream" };

        PathString[] apiv2 =
        {
            "/api/webui", "/api/version", "/plex", "/api/init", "/api/dev", "/api/modules", "/api/core",
            "/api/links", "/api/cast", "/api/group", "/api/filter", "/api/cloud", "/api/serie", "/api/ep",
            "/api/file", "/api/queue", "/api/myid", "/api/news", "/api/search", "/api/remove_missing_files",
            "/api/stats_update", "/api/medainfo_update", "/api/hash", "/api/rescan", "/api/rescanunlinked",
            "/api/folder", "/api/rescanmanuallinks", "/api/rehash", "/api/config", "/api/rehashunlinked",
            "/api/rehashmanuallinks", "/api/ep"
        };

        if (apiv1.Any(request.Path.StartsWithSegments))
        {
            return "1.0";
        }

        if (apiv2.Any(request.Path.StartsWithSegments))
        {
            return "2.0";
        }

        return "2.0"; //default to 2.0
    }
}
