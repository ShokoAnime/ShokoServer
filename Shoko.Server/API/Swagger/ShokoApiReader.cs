using System.Linq;
using Microsoft.AspNetCore.Http;
using Asp.Versioning;
using System.Collections.Generic;

namespace Shoko.Server.API.Swagger;

public class ShokoApiReader(bool enableV1, bool enableV2) : IApiVersionReader
{
    private static readonly PathString[] _v1 = ["/v1", "/api/Image", "/api/Kodi", "/api/Metro", "/api/Plex", "/Stream"];

    private static readonly PathString[] _v2 =
    [
        "/api/webui", "/api/version", "/plex", "/api/init", "/api/dev", "/api/modules", "/api/core",
        "/api/links", "/api/cast", "/api/group", "/api/filter", "/api/cloud", "/api/serie", "/api/ep",
        "/api/file", "/api/queue", "/api/myid", "/api/news", "/api/search", "/api/remove_missing_files",
        "/api/stats_update", "/api/medainfo_update", "/api/hash", "/api/rescan", "/api/rescanunlinked",
        "/api/folder", "/api/rescanmanuallinks", "/api/rehash", "/api/config", "/api/rehashunlinked",
        "/api/rehashmanuallinks", "/api/ep", "/api/ping", "/api/avdumpmismatchedfiles"
    ];
    public void AddParameters(IApiVersionParameterDescriptionContext context)
    {
        context.AddParameter(null, ApiVersionParameterLocation.Path);
    }

    public string Read(HttpRequest request)
    {
        if (enableV1 && _v1.Any(request.Path.StartsWithSegments))
            return "1.0";

        if (enableV2 && _v2.Any(request.Path.StartsWithSegments))
            return "2.0";

        return null; // defer to controller attribute or configured default
    }

    IReadOnlyList<string> IApiVersionReader.Read(HttpRequest request)
        => Read(request) is { } version ? [version] : [];
}
