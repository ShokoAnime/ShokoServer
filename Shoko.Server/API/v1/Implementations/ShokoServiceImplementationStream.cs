using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.API.Annotations;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
#nullable disable
namespace Shoko.Server.API.v1.Implementations;

[ApiInUse]
[ApiController]
[Route("/Stream")]
[ApiVersion("1.0", Deprecated = true)]
public class ShokoServiceImplementationStream(
    IVideoStreamPipelineService _streamPipelineService,
    VideoStreamSessionManager _streamSessionManager
) : Controller, IHttpContextAccessor
{
    public new HttpContext HttpContext { get; set; }

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public const string SERVER_VERSION = "Shoko Stream Server 1.0";

    [HttpGet("{videoLocalId}/{userId?}/{autoWatch?}/{fakeName?}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(FileStreamResult), 206)]
    [ProducesResponseType(404)]
    public async Task<object> StreamVideo(int videoLocalId, int? userId, bool? autoWatch, string fakeName)
    {
        var r = ResolveVideoLocal(videoLocalId, userId, autoWatch);
        if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent) return StatusCode((int)r.Status, r.StatusDescription);
        if (!string.IsNullOrEmpty(fakeName))
            return await TransformedResult(r, headOnly: false) ?? StreamInfoResult(r, autoWatch);

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

    /// <summary>
    ///   Serves this video through the selected <see cref="IVideoStreamTransform"/>, or returns
    ///   <c>null</c> if none applies and the caller should fall through to the raw file.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This route predates the stream pipeline and used to hand back the file on disk and
    ///     nothing else, so an enabled transform did nothing for any client still using it.
    ///     Everywhere else the pipeline works by minting a session and redirecting the client
    ///     to a session-scoped URL; that is not available here, because these routes are
    ///     unauthenticated and the v3 session route is not (unless
    ///     <c>Web.AllowAnonymousFileStreamingInAPIv3</c> is on), so the redirect would land on
    ///     a 401. The session is therefore looked up by a key composed from the request rather
    ///     than carried in it -- see <see cref="VideoStreamSessionManager.TryGetSessionByKey"/>
    ///     for what that costs.
    ///   </para>
    ///   <para>
    ///     Only progressive transforms can be served here. An HLS one needs a manifest and a
    ///     segment namespace, which this single-file route has nowhere to put, so it falls
    ///     through to the original file rather than pretending.
    ///   </para>
    /// </remarks>
    [NonAction]
    private async Task<object> TransformedResult(InfoResult r, bool headOnly)
    {
        // The by-filename routes resolve a path, not a library entry, so there is no IVideo to
        // select a transform for. Raw file is the only thing they can serve.
        if (r.VideoLocal is not { } video)
            return null;

        var context = new VideoStreamTransformContext { User = r.User, QueryParameters = Request.Query };
        VideoStreamTransformInfo transformInfo;
        try
        {
            transformInfo = _streamPipelineService.SelectTransform(video, context);
        }
        catch (Exception ex)
        {
            // Selection runs plugin code (SupportsVideo). A plugin that throws must not take
            // playback with it -- the original file is always a valid answer here.
            _logger.Error(ex, "Video stream transform selection failed for video {0}; serving the original file", video.VideoLocalID);
            return null;
        }

        if (transformInfo is null || transformInfo.Transform.DeliveryMode is not StreamDeliveryMode.Progressive)
            return null;

        // Keyed per video, per user and per transform: the finest distinction this route
        // offers. Two players sharing all three share a rendition and will fight over its
        // position; nothing in the URL can tell them apart.
        var key = $"v1:{video.VideoLocalID}:{r.User?.JMMUserID ?? 0}:{transformInfo.ID}";
        // Request.HttpContext, NOT this class's own HttpContext property: that one shadows
        // ControllerBase's with `new` (the class implements IHttpContextAccessor) and MVC
        // never assigns it, so it is null on every request. Reading the token from it would
        // compile, never throw, and silently never cancel -- leaving a rendition still being
        // read after the player has gone.
        var cancellationToken = Request.HttpContext.RequestAborted;
        StreamSession session;
        try
        {
            session = await _streamSessionManager.GetOrCreateSessionAsync(
                key,
                video,
                async token => await transformInfo.Transform.GetRenditionAsync(video, context, token),
                cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            // A transform that cannot start is worth reporting rather than silently papering
            // over: falling back to the original file here would look like the transform is
            // enabled and doing nothing, which is the hardest thing to diagnose.
            _logger.Error(ex, "Video stream transform \"{0}\" could not start a rendition for video {1}", transformInfo.Name, video.VideoLocalID);
            return StatusCode((int)HttpStatusCode.InternalServerError, $"Transform \"{transformInfo.Name}\" could not start a rendition: {ex.Message}");
        }

        if (session?.Rendition is not IProgressiveStreamRendition rendition)
            return StatusCode((int)HttpStatusCode.InternalServerError, $"Transform \"{transformInfo.Name}\" reports progressive delivery but its rendition does not implement IProgressiveStreamRendition.");

        // A rendition that will not declare a length cannot answer a byte range: a 206 has to
        // name a concrete last-byte-position and there is none. Serve it whole instead, the
        // same rule the v3 route applies.
        var totalSize = rendition.EstimatedTotalBytes;
        var rangeStart = totalSize is null ? null : ParseRangeStart(Request.Headers.Range.FirstOrDefault());

        Response.Headers.Append("Server", SERVER_VERSION);
        // Only when it can actually be honoured -- see below. Claiming range support and
        // then serving every request from byte 0 makes a seeking player restart playback
        // silently on each attempt, which looks like a stream that cannot keep up rather
        // than one that cannot seek.
        if (totalSize is not null)
            Response.Headers.Append("Accept-Ranges", "bytes");
        Response.ContentType = rendition.ContainerMimeType;
        if (totalSize is { } total)
        {
            var start = Math.Clamp(rangeStart ?? 0, 0, Math.Max(0, total - 1));
            if (rangeStart is not null)
            {
                Response.StatusCode = (int)HttpStatusCode.PartialContent;
                Response.Headers.Append("Content-Range", $"bytes {start}-{total - 1}/{total}");
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.OK;
            }
            Response.ContentLength = total - start;
        }
        else
        {
            Response.StatusCode = (int)HttpStatusCode.OK;
        }

        // HEAD wants the framing above and nothing else. Opening the stream would start the
        // conversion producing bytes for a body that is never sent.
        //
        // EmptyResult, not Ok(): OkResult is a StatusCodeResult and would write 200 over the
        // 206 just set above.
        if (headOnly)
            return new EmptyResult();

        Stream stream;
        try
        {
            stream = await rendition.OpenAsync(rangeStart, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }

        return stream ?? (object)StatusCode((int)HttpStatusCode.NotFound);
    }

    /// <summary>First byte of a Range header, or null. Only the start is meaningful against a
    /// stream still being produced -- an explicit end, or a suffix ("last N bytes"), asks about
    /// bytes whose existence nothing can vouch for yet.</summary>
    [NonAction]
    private static long? ParseRangeStart(string rangeValue)
    {
        if (string.IsNullOrEmpty(rangeValue))
            return null;

        var spec = rangeValue.Replace("bytes=", string.Empty).Split(',')[0];
        var dash = spec.IndexOf('-');
        if (dash <= 0)
            return null;

        return long.TryParse(spec[..dash].Trim(), out var start) && start >= 0 ? start : null;
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
            return outStream;
        }
        catch (Exception e)
        {
            _logger.Error("An error occurred while serving a file: " + e);
            return StatusCode(500, e.Message);
        }
    }

    [HttpHead("{videoLocalId}/{userId?}/{autoWatch?}/{fakeName?}")]
    public async Task<object> InfoVideo(int videoLocalId, int? userId, bool? autoWatch, string fakeName)
    {
        var r = ResolveVideoLocal(videoLocalId, userId, autoWatch);
        if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
        {
            return StatusCode((int)r.Status, r.StatusDescription);
        }

        // Must go through the transform too, or a player probes the ORIGINAL file's size and
        // then requests ranges against a stream of an entirely different length.
        if (await TransformedResult(r, headOnly: true) is { } transformed)
            return transformed;

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
        public VideoLocal VideoLocal { get; set; }
        public JMMUser User { get; set; }
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
        r.File = loc.FirstResolvedPlace?.FileInfo;
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

        r.Mime = ContentTypeHelper.GetContentType(r.File.FullName);
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
