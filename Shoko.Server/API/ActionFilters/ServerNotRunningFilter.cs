using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Web.Attributes;

namespace Shoko.Server.API.ActionFilters;

public class ServerNotRunningMiddleware(RequestDelegate next, ISystemService systemService)
{
    private const string NewInstance = "The server is in setup mode. Use the First Time Setup Wizard or check your settings and logs to ensure the server is starting correctly.";
    private const string ExistingInstance = "The server is starting up. Check your logs to ensure the server is starting correctly.";
    private const string FailedStartup = "The server failed to start. Check your logs for more information.";

    public async Task Invoke(HttpContext context)
    {
        if (!systemService.IsStarted)
        {
            var endpoint = context.GetEndpoint();
            var exempt = endpoint?.Metadata.GetMetadata<InitFriendlyAttribute>() is not null;
            if (!exempt)
            {
                var message = systemService.StartupFailedException is not null
                    ? FailedStartup
                    : systemService.InSetupMode
                        ? NewInstance
                        : ExistingInstance;

                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                await context.Response.WriteAsync(message);
                return;
            }
        }

        await next(context);
    }
}
