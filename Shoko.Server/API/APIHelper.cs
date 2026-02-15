using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.Enums;
using Shoko.Server.API.Authentication;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.API;

public static class APIHelper
{
    public static string ConstructImageLinkFromTypeAndId(HttpContext ctx, ImageEntityType imageType, DataSource dataType, int id, bool short_url = true)
         => ProperURL(ctx, $"/api/v3/Image/{dataType.ToV3Dto()}/{imageType.ToV3Dto()}/{id}", short_url);

    public static string ProperURL(HttpContext ctx, string path, bool short_url = false)
    {
        if (!string.IsNullOrEmpty(path))
        {
            return !short_url
                ? ctx.Request.Scheme + "://" + ctx.Request.Host.Host + ":" + ctx.Request.Host.Port + path
                : path;
        }

        return string.Empty;
    }

    // Only get the user once from the db for the same request, and let the GC
    // automagically clean up the user object reference mapping when the request
    // is disposed.
    private static readonly ConditionalWeakTable<ClaimsPrincipal, JMMUser> _userTable = [];

    public static JMMUser GetUser(this ClaimsPrincipal identity)
    {
        if (!ServerState.Instance.ServerOnline) return InitUser.Instance;

        var authenticated = identity?.Identity?.IsAuthenticated ?? false;
        if (!authenticated) return null;
        if (_userTable.TryGetValue(identity, out var user))
            return user;

        var nameIdentifier = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (nameIdentifier == null) return null;
        if (!int.TryParse(nameIdentifier, out var id) || id == 0) return null;
        user = RepoFactory.JMMUser.GetByID(id);
        _userTable.AddOrUpdate(identity, user);
        return user;
    }

    public static JMMUser GetUser(this HttpContext ctx)
    {
        return ctx.User.GetUser();
    }

    public static (string, string) GetToken(this HttpContext ctx)
    {
        var token = ctx.User.Claims.FirstOrDefault(c => c.Type == "apikey")?.Value;
        var device = ctx.User.Claims.FirstOrDefault(c => c.Type == "apikey.device")?.Value;
        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(device))
            return (token, device);

        return (null, null);
    }
}
