using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Server.API.Annotations;

namespace Shoko.Server.API.ActionFilters
{
    public class ServerNotRunningFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var exempt = context.ActionDescriptor.EndpointMetadata.OfType<InitFriendlyAttribute>().Any();
            if (!ServerState.Instance.ServerOnline && !exempt)
                context.Result = new BadRequestObjectResult("The Server is not running. Use the First Time Setup Wizard or check your settings and logs to ensure the server is starting correctly.");
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            
        }
    }
}