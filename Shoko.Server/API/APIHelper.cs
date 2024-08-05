﻿using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Authentication;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.API;

public static class APIHelper
{
    public static string ConstructImageLinkFromTypeAndId(HttpContext ctx, ImageEntityType imageType, DataSourceEnum dataType, int id, bool short_url = true)
         => ProperURL(ctx, $"/api/v3/image/{imageType.ToV3Dto()}/{dataType.ToV3Dto()}/{id}", short_url);

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

    public static SVR_JMMUser GetUser(this ClaimsPrincipal identity)
    {
        if (!ServerState.Instance.ServerOnline) return InitUser.Instance;

        var authenticated = identity?.Identity?.IsAuthenticated ?? false;
        if (!authenticated) return null;

        var nameIdentifier = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (nameIdentifier == null) return null;
        if (!int.TryParse(nameIdentifier, out var id) || id == 0) return null;
        return RepoFactory.JMMUser.GetByID(id);
    }

    public static SVR_JMMUser GetUser(this HttpContext ctx)
    {
        return ctx.User.GetUser();
    }
}
