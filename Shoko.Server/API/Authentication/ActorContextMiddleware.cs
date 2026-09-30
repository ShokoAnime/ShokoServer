using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.API.Authentication;

/// <summary>
///   Makes the request's API token the actor of everything the request does,
///   and ends it with the request. Runs right after authentication.
/// </summary>
/// <param name="next">The rest of the pipeline.</param>
public class ActorContextMiddleware(RequestDelegate next)
{
    /// <summary>
    ///   Runs the rest of the pipeline for the request's token, or for no one
    ///   when the request is not authenticated with one.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="actorContext">Where the actor is set.</param>
    /// <param name="userService">Where the request's token is read.</param>
    /// <returns>A task that completes with the rest of the pipeline.</returns>
    public async Task Invoke(HttpContext context, IActorContext actorContext, IUserService userService)
    {
        var token = context.User.Identity?.IsAuthenticated is true ? userService.GetApiTokenFromHttpContext(context) : null;
        using (actorContext.BeginScope(token))
            await next(context);
    }
}
