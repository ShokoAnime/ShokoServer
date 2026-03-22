using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Web.Attributes;

namespace Shoko.Server.API.ActionFilters;

public class DatabaseBlockedFilter(ISystemService systemService) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var exempt = context.ActionDescriptor.EndpointMetadata.OfType<DatabaseBlockedExemptAttribute>().Any();
        if (systemService.IsDatabaseBlocked && !exempt)
        {
            context.Result = new BadRequestObjectResult("Database is Blocked");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
