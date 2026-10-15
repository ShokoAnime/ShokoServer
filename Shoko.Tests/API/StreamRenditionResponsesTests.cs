using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.API;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers how the stream routes answer what a rendition hands back or throws
/// when it is asked to open a resource or a byte range.
/// </summary>
public class StreamRenditionResponsesTests
{
    #region Helpers

    private static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(1);

    private static Task<StreamRenditionResponses.Opened<Stream>> Open(Func<CancellationToken, Task<Stream?>> open, HttpResponse response, TimeSpan? timeout = null, CancellationToken? requestAborted = null)
        => StreamRenditionResponses.OpenAsync(open, response, timeout ?? LongTimeout, "Timed out.", requestAborted ?? TestContext.Current.CancellationToken);

    private static Task<StreamRenditionResponses.Opened<Stream>> Throwing(Exception exception, HttpResponse response)
        => Open(_ => throw exception, response);

    #endregion

    #region Results

    [Fact]
    public async Task AStreamIsHandedBack()
    {
        using var stream = new MemoryStream();

        var opened = await Open(_ => Task.FromResult<Stream?>(stream), new DefaultHttpContext().Response);

        Assert.True(opened.IsOpen);
        Assert.Same(stream, opened.Value);
        Assert.Null(opened.Failure);
    }

    [Fact]
    public async Task NothingIsStillABareNotFound()
    {
        var opened = await Open(_ => Task.FromResult<Stream?>(null), new DefaultHttpContext().Response);

        Assert.False(opened.IsOpen);
        Assert.IsType<NotFoundResult>(opened.Failure);
    }

    #endregion

    #region Refusals

    [Theory]
    [InlineData(
        typeof(StreamResourceNotFoundException),
        typeof(NotFoundObjectResult),
        StatusCodes.Status404NotFound,
        "This file has no such track."
    )]
    [InlineData(
        typeof(StreamResourceUnsupportedException),
        typeof(BadRequestObjectResult),
        StatusCodes.Status400BadRequest,
        "This codec cannot be carried in MP4."
    )]
    [InlineData(
        typeof(StreamResourceException), // the base refusal
        typeof(ObjectResult),
        StatusCodes.Status500InternalServerError,
        "Something went wrong."
    )]
    public async Task ARefusalIsAnsweredWithItsStatusAndTheReason(Type exceptionType, Type resultType, int status, string reason)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, reason, null)!;

        var opened = await Throwing(exception, new DefaultHttpContext().Response);

        var result = Assert.IsAssignableFrom<ObjectResult>(opened.Failure);
        Assert.IsType(resultType, result);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(reason, result.Value);
    }

    [Fact]
    public async Task NotReadyIsA503WithRetryAfterRoundedUp()
    {
        var response = new DefaultHttpContext().Response;

        var opened = await Throwing(new StreamResourceNotReadyException("The segment is still being produced.", TimeSpan.FromSeconds(2.1)), response);

        var result = Assert.IsType<ObjectResult>(opened.Failure);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("The segment is still being produced.", result.Value);
        Assert.Equal("3", response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task NotReadyWithoutAWaitHasNoRetryAfter()
    {
        var response = new DefaultHttpContext().Response;

        var opened = await Throwing(new StreamResourceNotReadyException("The segment is still being produced."), response);

        var result = Assert.IsType<ObjectResult>(opened.Failure);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.False(response.Headers.ContainsKey("Retry-After"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1.001, 2)]
    [InlineData(-5, 0)]
    public void RetryAfterIsWholeSecondsRoundedUp(double seconds, long expected)
        => Assert.Equal(expected, StreamRenditionResponses.RetryAfterSeconds(TimeSpan.FromSeconds(seconds)));

    #endregion

    #region Unchanged behaviour

    [Theory]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(NotImplementedException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(IOException))]
    public async Task AnyOtherExceptionIsLeftAlone(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var response = new DefaultHttpContext().Response;

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => Throwing(exception, response));

        Assert.Same(exception, thrown);
        Assert.False(response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task TheTimeoutIsA504()
    {
        var opened = await Open(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        }, new DefaultHttpContext().Response, TimeSpan.FromMilliseconds(50));

        var result = Assert.IsType<ObjectResult>(opened.Failure);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, result.StatusCode);
        Assert.Equal("Timed out.", result.Value);
    }

    [Fact]
    public async Task AnAbortedRequestIsStillCancelled()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Open(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        }, new DefaultHttpContext().Response, requestAborted: aborted.Token));
    }

    #endregion
}
