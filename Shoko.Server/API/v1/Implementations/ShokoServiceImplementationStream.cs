using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Models.Interfaces;
using Shoko.Server.API.Annotations;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server;

[ApiInUse]
[ApiController]
[Route("/Stream")]
[ApiVersion("1.0", Deprecated = true)]
public class ShokoServiceImplementationStream : Controller, IShokoServerStream, IHttpContextAccessor
{
    public new HttpContext HttpContext { get; set; }

    //89% Should be enough to not touch mkv offsets and give us some margin
    private readonly double _watchedThreshold = 0.89;

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public const string SERVER_VERSION = "Shoko Stream Server 1.0";

    [HttpGet("{videoLocalId}/{userId?}/{autoWatch?}/{fakeName?}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(FileStreamResult), 206)]
    [ProducesResponseType(404)]
    public object StreamVideo(int videoLocalId, int? userId, bool? autoWatch, string fakeName)
    {
        var r = ResolveVideoLocal(videoLocalId, userId, autoWatch);
        if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent) return StatusCode((int)r.Status, r.StatusDescription);
        if (!string.IsNullOrEmpty(fakeName)) return StreamInfoResult(r, autoWatch);

        var subs = r.VideoLocal.MediaInfo.TextStreams.Where(a => a.External).ToList();
        if (subs.Count == 0) return StatusCode(404);

        return "<table>" + string.Join(string.Empty, subs.Select(a => "<tr><td><a href=\"" + a.Filename + "\"/></td></tr>")) + "</table>";
    }

    [HttpGet("Filename/{base64filename}/{userId?}/{autoWatch?}/{fakeName?}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(FileStreamResult), 206)]
    [ProducesResponseType(404)]
    public object StreamVideoFromFilename(string base64filename, int? userId, bool? autoWatch, string fakeName)
    {
        var r = ResolveFilename(base64filename, userId, autoWatch);
        if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
        {
            return StatusCode((int)r.Status, r.StatusDescription);
        }

        return StreamInfoResult(r, autoWatch);
    }

    [NonAction]
    private object StreamInfoResult(InfoResult r, bool? autoWatch)
    {
        try
        {
            var rangeValue = Request.Headers.Range.FirstOrDefault();
            Stream fr = null;
            string error = null;
            try
            {
                fr = r.File?.OpenRead();
            }
            catch (Exception e)
            {
                _logger.Error(e);
                error = e.ToString();
            }

            if (fr == null)
            {
                return StatusCode((int)HttpStatusCode.BadRequest,
                    "Unable to open file '" + r.File?.FullName + "': " + error);
            }

            var totalSize = fr.Length;
            long start = 0;
            var end = totalSize - 1;

            rangeValue = rangeValue?.Replace("bytes=", string.Empty);
            var range = !string.IsNullOrEmpty(rangeValue);

            if (range)
            {
                // range: bytes=split[0]-split[1]
                var split = rangeValue.Split('-');
                if (split.Length == 2)
                {
                    // bytes=-split[1] - tail of specified length
                    if (string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                    {
                        var e = long.Parse(split[1]);
                        start = totalSize - e;
                        end = totalSize - 1;
                    }
                    // bytes=split[0] - split[0] to end of file
                    else if (!string.IsNullOrEmpty(split[0]) && string.IsNullOrEmpty(split[1]))
                    {
                        start = long.Parse(split[0]);
                        end = totalSize - 1;
                    }
                    // bytes=split[0]-split[1] - specified beginning and end
                    else if (!string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                    {
                        start = long.Parse(split[0]);
                        end = long.Parse(split[1]);
                        if (start > totalSize - 1)
                        {
                            start = totalSize - 1;
                        }

                        if (end > totalSize - 1)
                        {
                            end = totalSize - 1;
                        }
                    }
                }
            }

            Response.ContentType = r.Mime;
            Response.Headers.Append("Server", SERVER_VERSION);
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("Accept-Ranges", "bytes");
            Response.Headers.Append("Content-Range", "bytes " + start + "-" + end + "/" + totalSize);
            Response.ContentLength = end - start + 1;

            Response.StatusCode = (int)(range ? HttpStatusCode.PartialContent : HttpStatusCode.OK);

            var outStream = new SubStream(fr, start, end - start + 1);
            if (r.User != null && autoWatch.HasValue && autoWatch.Value && r.VideoLocal != null)
            {
                outStream.CrossPosition = (long)(totalSize * _watchedThreshold);
                outStream.CrossPositionCrossed +=
                    a =>
                    {
                        Task.Factory.StartNew(async () =>
                            {
                                var watchedService = Utils.ServiceContainer.GetRequiredService<WatchedStatusService>();
                                await watchedService.SetWatchedStatus(r.VideoLocal, true, r.User.JMMUserID);
                            },
                            new CancellationToken(),
                            TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    };
            }

            return outStream;
        }
        catch (Exception e)
        {
            _logger.Error("An error occurred while serving a file: " + e);
            return StatusCode(500, e.Message);
        }
    }

    [HttpHead("{videoLocalId}/{userId?}/{autoWatch?}/{fakeName?}")]
    public object InfoVideo(int videoLocalId, int? userId, bool? autoWatch, string fakeName)
    {
        var r = ResolveVideoLocal(videoLocalId, userId, autoWatch);
        if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
        {
            return StatusCode((int)r.Status, r.StatusDescription);
        }

        Response.Headers.Append("Server", SERVER_VERSION);
        Response.Headers.Append("Accept-Ranges", "bytes");
        Response.Headers.Append("Content-Range", "bytes 0-" + (r.File.Length - 1) + "/" + r.File.Length);
        Response.ContentType = r.Mime;
        Response.ContentLength = r.File.Length;
        Response.StatusCode = (int)r.Status;
        return Ok();
    }

    [HttpHead("Filename/{base64filename}/{userId?}/{autoWatch?}/{fakeName?}")]
    public object InfoVideoFromFilename(string base64filename, int? userId, bool? autoWatch, string fakeName)
    {
        var r = ResolveFilename(base64filename, userId, autoWatch);
        if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
        {
            return StatusCode((int)r.Status, r.StatusDescription);
        }

        Response.Headers.Append("Server", SERVER_VERSION);
        Response.Headers.Append("Accept-Ranges", "bytes");
        Response.Headers.Append("Content-Range", "bytes 0-" + (r.File.Length - 1) + "/" + r.File.Length);
        Response.ContentType = r.Mime;
        Response.ContentLength = r.File.Length;
        Response.StatusCode = (int)r.Status;
        return Ok();
    }

    private class InfoResult
    {
        public FileInfo File { get; set; }
        public SVR_VideoLocal VideoLocal { get; set; }
        public SVR_JMMUser User { get; set; }
        public HttpStatusCode Status { get; set; }
        public string StatusDescription { get; set; }
        public string Mime { get; set; }
    }

    private static InfoResult ResolveVideoLocal(int videoLocalId, int? userId, bool? autoWatch)
    {
        var r = new InfoResult();
        var loc = RepoFactory.VideoLocal.GetByID(videoLocalId);
        if (loc == null)
        {
            r.Status = HttpStatusCode.BadRequest;
            r.StatusDescription = "Video Not Found";
            return r;
        }

        r.VideoLocal = loc;
        r.File = loc.FirstResolvedPlace?.GetFile();
        return FinishResolve(r, userId, autoWatch);
    }

    public static string Base64DecodeUrl(string base64EncodedData)
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData.Replace("-", "+").Replace("_", "/").Replace(",", "="));
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }

    private static InfoResult FinishResolve(InfoResult r, int? userId, bool? autoWatch)
    {
        if (r.File == null)
        {
            r.Status = HttpStatusCode.NotFound;
            r.StatusDescription = "Video Not Found";
            return r;
        }

        if (userId.HasValue && autoWatch.HasValue && userId.Value != 0)
        {
            r.User = RepoFactory.JMMUser.GetByID(userId.Value);
            if (r.User == null)
            {
                r.Status = HttpStatusCode.NotFound;
                r.StatusDescription = "User Not Found";
                return r;
            }
        }

        r.Mime = Mime.GetMimeMapping(r.File.FullName);
        r.Status = HttpStatusCode.OK;
        return r;
    }

    private static InfoResult ResolveFilename(string base64, int? userId, bool? autoWatch)
    {
        var r = new InfoResult();
        var fullName = Base64DecodeUrl(base64);
        r.VideoLocal = null;
        r.File = new FileInfo(fullName);
        return FinishResolve(r, userId, autoWatch);
    }
}
