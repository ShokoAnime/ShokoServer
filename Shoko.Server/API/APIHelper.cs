using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.API.v2.Models.common;
using Shoko.Server.API.v3;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Shoko.Server.API.Authentication;

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

        public static SVR_JMMUser GetUser(this ClaimsPrincipal identity)
        {
            if (!ServerState.Instance.ServerOnline)
                return InitUser.Instance;

            if (!(identity?.Identity?.IsAuthenticated ?? false)) return null;

            var nameIdentifier = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            return nameIdentifier == null ? null : RepoFactory.JMMUser.GetByID(int.Parse(nameIdentifier));
        }

        public static SVR_JMMUser GetUser(this HttpContext ctx) => ctx.User.GetUser();
    }
}