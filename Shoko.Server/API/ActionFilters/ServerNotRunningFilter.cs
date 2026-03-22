using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Web.Attributes;

namespace Shoko.Server.API.ActionFilters;

public class ServerNotRunningFilter(ISystemService systemService) : IActionFilter
{
    private const string NewInstance = "The server is in setup mode. Use the First Time Setup Wizard or check your settings and logs to ensure the server is starting correctly.";

    private const string ExistingInstance = "The server is starting up. Check your logs to ensure the server is starting correctly.";

    private const string FailedStartup = "The server failed to start. Check your logs for more information.";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var exempt = context.ActionDescriptor.EndpointMetadata.OfType<InitFriendlyAttribute>().Any();
        if (!systemService.IsStarted && !exempt)
        {
            if (systemService.StartupFailedException is not null)
            {
                context.Result = new BadRequestObjectResult(FailedStartup)
                {
                    StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                };
            }
            else if (systemService.InSetupMode)
            {
                context.Result = new BadRequestObjectResult(NewInstance)
                {
                    StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                };
            }
            else
            {
                context.Result = new BadRequestObjectResult(ExistingInstance)
                {
                    StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                };
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
