using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Server.API.Authentication;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.API;

public static class APIHelper
{
    public static string ConstructImageLinkFromTypeAndId(HttpContext ctx, IImage image, bool short_url = true)
         => ProperURL(ctx, $"/api/v3/Image/{image.ID}", short_url);

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
    private static readonly ConditionalWeakTable<ClaimsPrincipal, ShokoUser> _userTable = [];

    // Returns null when unauthenticated or before the server starts, but is declared non-nullable
    // because every call site treats it as guaranteed; widening it is a repo-wide change.
    public static ShokoUser GetUser(this ClaimsPrincipal? identity)
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<ISystemService>();
        if (!systemService.IsStarted)
        {
            // While a setup token is set, only a request the handler took it from is the init user.
            if ((systemService.InSetupMode || systemService.StartupFailedException is not null) &&
                (identity?.Identity?.IsAuthenticated is true || (systemService as SystemService)?.SetupToken.IsRequired is not true))
                return InitUser.Instance;
            return null!;
        }

        if (identity?.Identity?.IsAuthenticated is not true) return null!;
        if (_userTable.TryGetValue(identity, out var user))
            return user;

        var nameIdentifier = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (nameIdentifier == null) return null!;
        if (!int.TryParse(nameIdentifier, out var id) || id == 0) return null!;
        user = RepoFactory.ShokoUser.GetByID(id)!;
        _userTable.AddOrUpdate(identity, user);
        return user;
    }

    public static ShokoUser GetUser(this HttpContext ctx)
    {
        return ctx.User.GetUser();
    }

    public static string? GetToken(this HttpContext ctx)
    {
        var token = ctx.User.Claims.FirstOrDefault(c => c.Type == "apikey")?.Value;
        if (!string.IsNullOrEmpty(token))
            return token;

        return null;
    }
}
