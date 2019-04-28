using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.API.v2.Models.common;
using Shoko.Server.API.v3;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

namespace Shoko.Server.API
{
    public static class APIHelper
    {
        public static string ConstructImageLinkFromTypeAndId(HttpContext ctx, int type, int id, bool short_url = true)
        {
            var imgType = (ImageEntityType) type;
            return APIHelper.ProperURL(ctx,
                $"/api/v3/image/{Image.GetSourceFromType(imgType)}/{Image.GetSimpleTypeFromImageType(imgType)}/{id}",
                short_url);
        }

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

        public static SVR_JMMUser GetUser(this IIdentity identity)
        {
            if (!(identity?.IsAuthenticated ?? false)) return null;
            return RepoFactory.JMMUser.GetByUsername(identity.Name);
        }
        
        public static SVR_JMMUser GetUser(this HttpContext ctx)
        {
            var identity = ctx?.User?.Identity;
            if (!(identity?.IsAuthenticated ?? false)) return null;
            return RepoFactory.JMMUser.GetByUsername(identity.Name);
        }
    }
}