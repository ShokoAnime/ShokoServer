using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.API.ActionFilters;

/// <summary>
///   Reports the links an API call writes by hand as one change. What gives
///   its own reason, such as verifying, importing or deleting a series, is
///   reported on its own.
/// </summary>
/// <param name="linkChanges">Where the reason is set.</param>
public class ManualLinkChangeFilter(MetadataLinkChangeTracker linkChanges) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        using var reason = linkChanges.UseDefaultReason(MetadataLinkChangeReason.Manual);
        using var operation = linkChanges.Begin();
        await next().ConfigureAwait(false);
    }
}
