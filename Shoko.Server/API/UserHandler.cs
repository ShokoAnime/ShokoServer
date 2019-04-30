using Microsoft.AspNetCore.Authorization;
using Shoko.Server.Models;
using System;
using System.Threading.Tasks;

namespace Shoko.Server.API
{

    public class UserHandler : AuthorizationHandler<UserHandler>, IAuthorizationRequirement
    {
        readonly Func<SVR_JMMUser, bool> validationAction;

        public UserHandler(Func<SVR_JMMUser, bool> validationAction) => this.validationAction = validationAction;

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserHandler requirement)
        {
            if (context.User.GetUser() != null && requirement.validationAction(context.User.GetUser()))
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
