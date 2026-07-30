using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Video.Streaming;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// A <see cref="PhysicalFileResult"/> that notifies enabled
/// <see cref="IPlaybackObserver"/>s (e.g. a scrobble plugin) after the
/// response completes, inferring playback position from the requested byte
/// range the same way the response itself is served.
/// </summary>
public class ObservedFileResult : PhysicalFileResult
{
    private IVideo Video { get; }

    private IUser? User { get; }

    public ObservedFileResult(IVideo video, IUser? user, string fileName, string contentType) : base(fileName, contentType)
    {
        Video = video;
        User = user;
        EnableRangeProcessing = true;
    }

    public ObservedFileResult(IVideo video, IUser? user, string fileName, MediaTypeHeaderValue contentType) : base(fileName, contentType)
    {
        Video = video;
        User = user;
        EnableRangeProcessing = true;
    }

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        await base.ExecuteResultAsync(context);

        var (start, end) = GetRange(context.HttpContext, Video.Size);
        var pipelineService = ISystemService.StaticServices.GetRequiredService<IVideoStreamPipelineService>();
        var progressContext = new PlaybackProgressContext
        {
            Video = Video,
            User = User,
            QueryParameters = context.HttpContext.Request.Query,
            Kind = PlaybackKind.Progressive,
            RangeStart = start,
            RangeEnd = end,
            TotalBytes = Video.Size,
            IsFinalUnit = end == Video.Size - 1,
        };

#pragma warning disable CS4014 // Fire-and-forget; observers should not delay or fail the response.
        Task.Factory.StartNew(() => pipelineService.NotifyPlaybackProgress(progressContext), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
#pragma warning restore CS4014
    }

    private static (long Start, long End) GetRange(HttpContext context, long length)
    {
        if (length == 0) return (0, 0);
        var requestHeaders = context.Request.GetTypedHeaders();
        var rangeHeader = requestHeaders.Range;
        if (rangeHeader == null) return (0, length - 1);
        var ranges = rangeHeader.Ranges;
        if (ranges.Count == 0) return (0, length - 1);

        var range = ranges.First();
        var start = range.From;
        var end = range.To;

        // X-[Y]
        if (start.HasValue)
        {
            if (start.Value >= length)
            {
                // Not satisfiable, skip/discard.
                return (0, length - 1);
            }
            if (!end.HasValue || end.Value >= length)
            {
                end = length - 1;
            }
        }
        else if (end.HasValue)
        {
            // suffix range "-X" e.g. the last X bytes, resolve
            if (end.Value == 0)
            {
                // Not satisfiable, skip/discard.
                return (0, length - 1);
            }

            var bytes = Math.Min(end.Value, length);
            start = length - bytes;
            end = start + bytes - 1;
        }

        return (start ?? 0, end ?? length - 1);
    }
}
