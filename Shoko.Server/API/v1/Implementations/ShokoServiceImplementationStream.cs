using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Models.Interfaces;
using Shoko.Server.API.Annotations;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server
{
    [ApiInUse]
    [ApiController, Route("/Stream"), ApiVersion("1.0", Deprecated = true)]
    public class ShokoServiceImplementationStream : Controller, IShokoServerStream, IHttpContextAccessor
    {
        public new HttpContext HttpContext { get; set; }

        //89% Should be enough to not touch matroska offsets and give us some margin
        private double WatchedThreshold = 0.89;

        public const string SERVER_VERSION = "Shoko Stream Server 1.0";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [HttpGet("{videolocalid}/{userId?}/{autowatch?}/{fakename?}")]
        [ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(typeof(FileStreamResult),206), ProducesResponseType(404)]
        public object StreamVideo(int videolocalid, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveVideoLocal(videolocalid, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
            {
                return StatusCode((int) r.Status, r.StatusDescription);
            }
            return StreamFromIFile(r, autowatch);
        }

        [HttpGet("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}")]
        [ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(typeof(FileStreamResult),206), ProducesResponseType(404)]
        public object StreamVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveFilename(base64filename, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
            {
                return StatusCode((int) r.Status, r.StatusDescription);
            }
            return StreamFromIFile(r, autowatch);
        }

        private object StreamFromIFile(InfoResult r, bool? autowatch)
        {
            try
            { 
                string rangevalue = Request.Headers["Range"].FirstOrDefault() ??
                                    Request.Headers["range"].FirstOrDefault();


                Stream fr = null;
                string error = null;
                try
                {
                    fr = r.File?.OpenRead();
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    error = e.ToString();
                }

                if (fr == null)
                {
                    return StatusCode((int) HttpStatusCode.BadRequest,
                        "Unable to open file '" + r.File?.FullName + "': " + error);
                }
                long totalsize = fr.Length;
                long start = 0;
                long end = totalsize - 1;

                rangevalue = rangevalue?.Replace("bytes=", string.Empty);
                bool range = !string.IsNullOrEmpty(rangevalue);

                if (range)
                {
                    // range: bytes=split[0]-split[1]
                    string[] split = rangevalue.Split('-');
                    if (split.Length == 2)
                    {
                        // bytes=-split[1] - tail of specified length
                        if (string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                        {
                            long e = long.Parse(split[1]);
                            start = totalsize - e;
                            end = totalsize - 1;
                        }
                        // bytes=split[0] - split[0] to end of file
                        else if (!string.IsNullOrEmpty(split[0]) && string.IsNullOrEmpty(split[1]))
                        {
                            start = long.Parse(split[0]);
                            end = totalsize - 1;
                        }
                        // bytes=split[0]-split[1] - specified beginning and end
                        else if (!string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                        {
                            start = long.Parse(split[0]);
                            end = long.Parse(split[1]);
                            if (start > totalsize - 1)
                                start = totalsize - 1;
                            if (end > totalsize - 1)
                                end = totalsize - 1;
                        }
                    }
                }

                Response.ContentType = r.Mime;
                Response.Headers.Add("Server", SERVER_VERSION);
                Response.Headers.Add("Connection", "keep-alive");
                Response.Headers.Add("Accept-Ranges", "bytes");
                Response.Headers.Add("Content-Range", "bytes " + start + "-" + end + "/" + totalsize);
                Response.ContentLength = end - start + 1;

                Response.StatusCode = (int)(range ? HttpStatusCode.PartialContent : HttpStatusCode.OK);

                var outstream = new SubStream(fr, start, end - start + 1);
                if (r.User != null && autowatch.HasValue && autowatch.Value && r.VideoLocal != null)
                {
                    outstream.CrossPosition = (long) (totalsize * WatchedThreshold);
                    outstream.CrossPositionCrossed +=
                        a =>
                        {
                            Task.Factory.StartNew(() => { r.VideoLocal.ToggleWatchedStatus(true, r.User.JMMUserID); },
                                new CancellationToken(),
                                TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        };
                }

                return outstream;
            }
            catch (Exception e)
            {
                Logger.Error("An error occurred while serving a file: " + e);
                return StatusCode(500, e.Message);
            }
        }

        [HttpHead("{videolocalid}/{userId?}/{autowatch?}/{fakename?}")]
        public object InfoVideo(int videolocalid, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveVideoLocal(videolocalid, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
                return StatusCode((int) r.Status, r.StatusDescription);
            Response.Headers.Add("Server", SERVER_VERSION);
            Response.Headers.Add("Accept-Ranges", "bytes");
            Response.Headers.Add("Content-Range", "bytes 0-" + (r.File.Length - 1) + "/" + r.File.Length);
            Response.ContentType = r.Mime;
            Response.ContentLength = r.File.Length;
            Response.StatusCode = (int)r.Status;
            return Ok();
        }

        [HttpHead("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}")]
        public object InfoVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveFilename(base64filename, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
                return StatusCode((int) r.Status, r.StatusDescription);
            Response.Headers.Add("Server", SERVER_VERSION);
            Response.Headers.Add("Accept-Ranges", "bytes");
            Response.Headers.Add("Content-Range", "bytes 0-" + (r.File.Length - 1) + "/" + r.File.Length);
            Response.ContentType = r.Mime;
            Response.ContentLength = r.File.Length;
            Response.StatusCode = (int)r.Status;
            return Ok();
        }

        class InfoResult
        {
            public FileInfo File { get; set; }
            public SVR_VideoLocal VideoLocal { get; set; }
            public SVR_JMMUser User { get; set; }
            public HttpStatusCode Status { get; set; }
            public string StatusDescription { get; set; }
            public string Mime { get; set; }
        }

        private InfoResult ResolveVideoLocal(int videolocalid, int? userId, bool? autowatch)
        {
            InfoResult r = new InfoResult();
            SVR_VideoLocal loc = RepoFactory.VideoLocal.GetByID(videolocalid);
            if (loc == null)
            {
                r.Status = HttpStatusCode.BadRequest;
                r.StatusDescription = "Video Not Found";
                return r;
            }
            r.VideoLocal = loc;
            r.File = loc.GetBestFileLink();
            return FinishResolve(r, userId, autowatch);
        }

        public static string Base64DecodeUrl(string base64EncodedData)
        {
            var base64EncodedBytes =
                Convert.FromBase64String(base64EncodedData.Replace("-", "+")
                    .Replace("_", "/")
                    .Replace(",", "="));
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        private InfoResult FinishResolve(InfoResult r, int? userId, bool? autowatch)
        {
            if (r.File == null)
            {
                r.Status = HttpStatusCode.NotFound;
                r.StatusDescription = "Video Not Found";
                return r;
            }
            if (userId.HasValue && autowatch.HasValue && userId.Value != 0)
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

        private InfoResult ResolveFilename(string filenamebase64, int? userId, bool? autowatch)
        {
            InfoResult r = new InfoResult();
            string fullname = Base64DecodeUrl(filenamebase64);
            r.VideoLocal = null;
            r.File = new FileInfo(fullname);
            return FinishResolve(r, userId, autowatch);
        }
    }
}