using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Helpers;

namespace Shoko.Server.API.Annotations;

/// <summary>
///   Answers <c>502 Bad Gateway</c> with a <c>Retry-After</c> header when an
///   action throws a <see cref="MetadataProviderUnavailableException"/>, so a
///   caller backs off from a provider that is down instead of retrying at
///   once.
/// </summary>
/// <remarks>
///   The wait is the exception's, else the rest of the source's pause, else a
///   minute. A <see cref="MetadataProviderNotConfiguredException"/> answers
///   <c>503 Service Unavailable</c> without a <c>Retry-After</c>, as waiting
///   does not configure it. Applied to every controller, plugins' included.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class MetadataProviderUnavailableAttribute : ExceptionFilterAttribute
{
    /// <summary>
    ///   Turns a <see cref="MetadataProviderUnavailableException"/> into a
    ///   <c>502</c> answer, or a <c>503</c> one for a source not configured.
    /// </summary>
    /// <param name="context">What the action threw.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
    public override void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ExceptionHandled || Unwrap(context.Exception) is not MetadataProviderUnavailableException exception)
            return;

        if (exception is MetadataProviderNotConfiguredException)
        {
            context.HttpContext.RequestServices?.GetService<ILogger<MetadataProviderUnavailableAttribute>>()?.LogInformation(
                "{Source} is not configured ({Message}). Refusing the request.",
                exception.MetadataSource.Name,
                exception.Message
            );
            context.Result = MetadataPauseResponses.Problem($"{exception.MetadataSource.Name} is not configured.", exception.Message, exception.MetadataSource, null);
            context.ExceptionHandled = true;
            return;
        }

        var seconds = RetryAfterSeconds(exception, context.HttpContext.RequestServices?.GetService<IMetadataRefreshService>());
        context.HttpContext.RequestServices?.GetService<ILogger<MetadataProviderUnavailableAttribute>>()?.LogInformation(
            "{Source} is unavailable ({Message}). Refusing the request; retry in about {Seconds} second(s).",
            exception.MetadataSource.Name,
            exception.Message,
            seconds
        );
        MetadataPauseResponses.SetRetryAfter(context.HttpContext.Response, seconds);
        context.Result = Problem(StatusCodes.Status502BadGateway, $"{exception.MetadataSource.Name} is currently unavailable.", exception.Message);
        context.ExceptionHandled = true;
    }

    /// <summary>
    ///   The failure an action threw, taken out of an
    ///   <see cref="AggregateException"/> holding only it, as a blocking wait
    ///   on a task throws.
    /// </summary>
    /// <param name="exception">What the action threw.</param>
    /// <returns>The single failure inside, or <paramref name="exception"/> itself.</returns>
    internal static Exception? Unwrap(Exception? exception)
    {
        while (exception is AggregateException { InnerExceptions: [var inner] })
            exception = inner;
        return exception;
    }

    /// <summary>
    ///   A problem answer.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="title">The short summary.</param>
    /// <param name="detail">What went wrong.</param>
    /// <returns>The answer.</returns>
    private static ObjectResult Problem(int status, string title, string detail)
        => new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };

    /// <summary>
    ///   How long a caller should wait before asking again.
    /// </summary>
    /// <param name="exception">What the provider threw.</param>
    /// <param name="refreshService">Tells what is left of the source's pause, when available.</param>
    /// <returns>The seconds to wait.</returns>
    internal static int RetryAfterSeconds(MetadataProviderUnavailableException exception, IMetadataRefreshService? refreshService)
    {
        if (exception.RetryAfter is { } retryAfter)
            return (int)Math.Ceiling(Math.Max(0, retryAfter.TotalSeconds));

        if (refreshService?.GetPauseStatus(exception.MetadataSource) is { IsPaused: true } status && MetadataPauseResponses.RetryAfterSeconds(status) is > 0 and var left)
            return left;

        return MetadataPauseResponses.DefaultRetryAfterSeconds;
    }
}
