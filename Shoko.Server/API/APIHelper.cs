using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shoko.Models.Enums;
using Shoko.Server.API.Authentication;
using Shoko.Server.API.v3;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API
{
    public static class APIHelper
    {
        public static string ConstructImageLinkFromTypeAndId(HttpContext ctx, int type, int id, bool short_url = true)
        {
            var imgType = (ImageEntityType) type;
            return ProperURL(ctx,
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