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
///   <see cref="IProgressiveStreamRendition.EstimatedTotalBytes"/> is only
///   ever an estimate (real output bitrate is rarely perfectly constant), so
///   the <c>Content-Length</c>/<c>Content-Range</c> total this declares may
///   not exactly match the number of bytes the underlying rendition actually
///   produces before its stream ends. That number comes from the rendition
///   and is passed through untouched: it is the scale the rendition's own
///   byte-to-time mapping (and, where one exists, the served container's seek
///   index) was built at, so a total computed here instead would put every
///   seek out by the ratio between the two.
///
///   If the rendition's stream ends before the declared length, the response
///   body simply ends short. <c>null</c> means the rendition declined to
///   declare a length at all, in which case there is no <c>Content-Length</c>
///   and no <c>Range</c> handling -- see the interface's own remarks for why
///   those two go together.
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
        // Only claim range support when it can actually be honoured. Advertising it while
        // serving every request from byte 0 is worse than not advertising it: the client
        // keeps seeking, keeps getting a 200 with the start of the stream, and silently
        // restarts playback each time -- which presents as a stream that cannot keep up
        // rather than as a stream that cannot seek.
        if (estimatedTotalBytes is not null)
            response.Headers.AcceptRanges = "bytes";
        response.ContentType = contentType;

        var start = rangeStart ?? 0;
        // The two halves go together deliberately. A 206 must name a concrete
        // last-byte-position -- "bytes 500-*/*" is not expressible -- so a range is only
        // ever honoured when the rendition declared a length to derive it from, which is
        // the same condition the controller applies before it forwards one.
        if (rangeStart is not null && estimatedTotalBytes is { } total)
        {
            response.StatusCode = StatusCodes.Status206PartialContent;
            response.Headers.ContentRange = $"bytes {start}-{total - 1}/{total}";
            response.ContentLength = Math.Max(0, total - start);
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
