using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.API;

public class UserHandler : AuthorizationHandler<UserHandler>, IAuthorizationRequirement
{
    private readonly Func<JMMUser, bool> validationAction;

    public UserHandler(Func<JMMUser, bool> validationAction)
    {
        this.validationAction = validationAction;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserHandler requirement)
    {
        if (context.User.GetUser() != null && requirement.validationAction(context.User.GetUser()))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
