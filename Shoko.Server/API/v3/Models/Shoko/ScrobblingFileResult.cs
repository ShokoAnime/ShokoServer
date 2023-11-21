using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Shoko.Server.Models;

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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Factory.StartNew(() => VideoLocal.ToggleWatchedStatus(true, UserID), new CancellationToken(), TaskCreationOptions.LongRunning,
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            TaskScheduler.Default);
    }
}
