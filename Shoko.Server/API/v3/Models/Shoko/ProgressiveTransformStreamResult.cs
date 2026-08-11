using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Video.Streaming;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
///   Serves an <see cref="IProgressiveStreamRendition"/>'s output as an HTTP
///   byte-range response, and notifies enabled <see cref="IPlaybackObserver"/>s
///   afterward -- the progressive-delivery-mode counterpart to
///   <see cref="ObservedFileResult"/>, which only works against a complete,
///   already-known-length physical file and so can't be reused here.
/// </summary>
/// <remarks>
///   <see cref="IProgressiveStreamRendition.EstimatedBytesPerSecond"/> is only
///   ever an estimate (real output bitrate is rarely perfectly constant), so
///   the <c>Content-Length</c>/<c>Content-Range</c> total this declares may
///   not exactly match the number of bytes the underlying rendition actually
///   produces before its stream ends -- a disclosed, not-yet-real-world-
///   validated trade-off of this delivery mode (see
///   <see cref="IProgressiveStreamRendition"/>'s own remarks). If the
///   rendition's stream ends before the declared length, the response body
///   simply ends short; if a client/player handles that poorly, tightening
///   this (e.g. falling back to an unbounded <c>Content-Range: .../*</c> when
///   duration is uncertain) is the first thing to try.
/// </remarks>
public class ProgressiveTransformStreamResult(
    IVideo video,
    IUser? user,
    Stream stream,
    string contentType,
    long? rangeStart,
    long? estimatedTotalBytes) : ActionResult
{
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.Headers.AcceptRanges = "bytes";
        response.ContentType = contentType;

        var start = rangeStart ?? 0;
        if (rangeStart is not null)
        {
            response.StatusCode = StatusCodes.Status206PartialContent;
            var totalText = estimatedTotalBytes is { } total ? total.ToString() : "*";
            var endText = estimatedTotalBytes is { } declaredEnd ? (declaredEnd - 1).ToString() : "*";
            response.Headers.ContentRange = $"bytes {start}-{endText}/{totalText}";
            if (estimatedTotalBytes is { } length)
                response.ContentLength = Math.Max(0, length - start);
        }
        else
        {
            response.StatusCode = StatusCodes.Status200OK;
            if (estimatedTotalBytes is { } length)
                response.ContentLength = length;
        }

        var requestAborted = context.HttpContext.RequestAborted;
        try
        {
            await using (stream)
            {
                await stream.CopyToAsync(response.Body, requestAborted).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
        {
            // Client disconnected/aborted -- nothing more to do.
            return;
        }

        var end = estimatedTotalBytes is { } totalBytes ? totalBytes - 1 : start;
        var progressContext = new PlaybackProgressContext
        {
            Video = video,
            User = user,
            QueryParameters = context.HttpContext.Request.Query,
            Kind = PlaybackKind.Progressive,
            RangeStart = start,
            RangeEnd = end,
            TotalBytes = estimatedTotalBytes,
            IsFinalUnit = estimatedTotalBytes is { } tb && end >= tb - 1,
        };

        var pipelineService = ISystemService.StaticServices.GetRequiredService<IVideoStreamPipelineService>();
#pragma warning disable CS4014 // Fire-and-forget; observers should not delay or fail the response.
        Task.Factory.StartNew(() => pipelineService.NotifyPlaybackProgress(progressContext), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
#pragma warning restore CS4014
    }
}
