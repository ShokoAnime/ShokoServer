using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Server.API.Annotations;

namespace Shoko.Server.API.ActionFilters
{
    public class DatabaseBlockedFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var exempt = context.ActionDescriptor.EndpointMetadata.OfType<DatabaseBlockedExemptAttribute>().Any();
            if (ServerState.Instance.DatabaseBlocked.Blocked && !exempt)
                context.Result = new BadRequestObjectResult("Database is Blocked");
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            
        }
    }
}