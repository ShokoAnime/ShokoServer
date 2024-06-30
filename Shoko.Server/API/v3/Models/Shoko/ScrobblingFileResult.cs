using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Shoko.Server.Models;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v3.Models.Shoko;

public class ScrobblingFileResult : PhysicalFileResult
{
    private SVR_VideoLocal VideoLocal { get; set; }
    private int UserID { get; set; }
    public ScrobblingFileResult(SVR_VideoLocal videoLocal, int userID, string fileName, string contentType) : base(fileName, contentType)
    {
        VideoLocal = videoLocal;
        UserID = userID;
        EnableRangeProcessing = true;
    }

    public ScrobblingFileResult(SVR_VideoLocal videoLocal, int userID, string fileName, MediaTypeHeaderValue contentType) : base(fileName, contentType)
    {
        VideoLocal = videoLocal;
        UserID = userID;
        EnableRangeProcessing = true;
    }

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        await base.ExecuteResultAsync(context);
        var end = GetRange(context.HttpContext, VideoLocal.FileSize);
        if (end != VideoLocal.FileSize) return;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        var watchedService = Utils.ServiceContainer.GetRequiredService<WatchedStatusService>();
        Task.Factory.StartNew(() => watchedService.SetWatchedStatus(VideoLocal, true, UserID), new CancellationToken(), TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    }

    private static long GetRange(HttpContext context, long length)
    {
        if (length == 0) return 0;
        var requestHeaders = context.Request.GetTypedHeaders();
        var rangeHeader = requestHeaders.Range;
        if (rangeHeader == null) return length;
        var ranges = rangeHeader.Ranges;
        if (ranges.Count == 0) return length;

        var range = ranges.First();
        var start = range.From;
        var end = range.To;

        // X-[Y]
        if (start.HasValue)
        {
            if (start.Value >= length)
            {
                // Not satisfiable, skip/discard.
                return length;
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
                return length;
            }

            var bytes = Math.Min(end.Value, length);
            start = length - bytes;
            end = start + bytes - 1;
        }

        return end ?? length;
    }
}
