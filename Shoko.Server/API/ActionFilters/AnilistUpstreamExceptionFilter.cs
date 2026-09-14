using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anilist.Services;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.API.ActionFilters;

/// <summary>
/// Maps a transient AniList failure raised while an endpoint talked to the
/// upstream directly (immediate refreshes, online lookups) to a 502 with a
/// <c>Retry-After</c> header, so callers can back off instead of retrying.
/// Queued jobs never reach this; they re-queue through the acquisition filter.
/// </summary>
public class AnilistUpstreamExceptionFilter(ILogger<AnilistUpstreamExceptionFilter> logger, IAnilistMetadataService anilistMetadataService) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AnilistApiException { IsTransient: true } exception)
            return;

        var status = anilistMetadataService.GetPauseStatus();
        var seconds = (int)(status.RemainingPauseTime?.TotalSeconds ?? 60);
        logger.LogInformation("AniList is unavailable ({Message}). Refusing the request; retry in approximately {Seconds} second(s).", exception.Message, seconds);
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString();
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = (int)HttpStatusCode.BadGateway,
            Title = "AniList is currently unavailable.",
            Detail = exception.Message,
        })
        {
            StatusCode = (int)HttpStatusCode.BadGateway,
        };
        context.ExceptionHandled = true;
    }
}
