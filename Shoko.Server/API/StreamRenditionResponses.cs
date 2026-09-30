using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Video.Streaming;

namespace Shoko.Server.API;

/// <summary>
/// The answers the stream routes give when a rendition cannot hand over what
/// was asked for: a timeout, a refusal, or nothing at all.
/// </summary>
public static class StreamRenditionResponses
{
    #region Opening

    /// <summary>
    /// What <see cref="OpenAsync{T}"/> opened, or the answer to give instead.
    /// </summary>
    /// <typeparam name="T">What is opened.</typeparam>
    /// <param name="Value">What was opened, if anything.</param>
    /// <param name="Failure">The answer to give instead, if nothing was opened.</param>
    public readonly record struct Opened<T>(T? Value, ActionResult? Failure) where T : class
    {
        /// <summary>
        /// Whether something was opened, i.e. <see cref="Value"/> is set.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Failure))]
        public bool IsOpen { get => Value is not null; }
    }

    /// <summary>
    /// Opens something of a rendition within the segment timeout, turning a
    /// timeout, a refusal and a missing result into answers.
    /// </summary>
    /// <typeparam name="T">What is opened.</typeparam>
    /// <param name="open">Opens it, given the request's token linked to the timeout.</param>
    /// <param name="response">The answer a refusal's headers are set on.</param>
    /// <param name="timeout">How long to wait before answering <c>504</c>.</param>
    /// <param name="timeoutReason">The reason given with a <c>504</c>.</param>
    /// <param name="requestAborted">The request's cancellation token.</param>
    /// <returns>
    /// What was opened, or the answer to give instead: <c>504</c> on the
    /// timeout, <c>404</c> for <c>null</c>, or the answer to a refusal.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="requestAborted"/> was cancelled before the timeout.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="open"/> or <paramref name="response"/> is <see langword="null"/>.</exception>
    public static async Task<Opened<T>> OpenAsync<T>(
        Func<CancellationToken, Task<T?>> open,
        HttpResponse response,
        TimeSpan timeout,
        string timeoutReason,
        CancellationToken requestAborted
    ) where T : class
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(response);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, timeoutCts.Token);

        T? value;
        try
        {
            value = await open(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return new(null, new ObjectResult(timeoutReason) { StatusCode = StatusCodes.Status504GatewayTimeout });
        }
        catch (StreamResourceException ex)
        {
            return new(null, Refused(response, ex));
        }

        return new(value, value is null ? new NotFoundResult() : null);
    }

    #endregion

    #region Refusals

    /// <summary>
    /// The answer to a refusal thrown by a rendition, with its message as the
    /// reason: <c>404</c> for not found, <c>400</c> for unsupported, and
    /// <c>503</c> with any <c>Retry-After</c> for not ready. Any other kind is
    /// a <c>500</c>.
    /// </summary>
    /// <param name="response">The answer the <c>Retry-After</c> header is set on.</param>
    /// <param name="exception">The refusal.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="exception"/> is <see langword="null"/>.</exception>
    public static ObjectResult Refused(HttpResponse response, StreamResourceException exception)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            case StreamResourceNotFoundException:
                return new NotFoundObjectResult(exception.Message);

            case StreamResourceUnsupportedException:
                return new BadRequestObjectResult(exception.Message);

            case StreamResourceNotReadyException notReady:
                if (notReady.RetryAfter is { } retryAfter)
                    response.Headers.RetryAfter = RetryAfterSeconds(retryAfter).ToString(CultureInfo.InvariantCulture);
                return new ObjectResult(exception.Message) { StatusCode = StatusCodes.Status503ServiceUnavailable };

            default:
                return new ObjectResult(exception.Message) { StatusCode = StatusCodes.Status500InternalServerError };
        }
    }

    /// <summary>
    /// How many whole seconds a client should wait, rounded up.
    /// </summary>
    /// <param name="retryAfter">The wait.</param>
    /// <returns>The seconds, never negative.</returns>
    public static long RetryAfterSeconds(TimeSpan retryAfter)
        => Math.Max(0, (long)Math.Ceiling(retryAfter.TotalSeconds));

    #endregion
}
