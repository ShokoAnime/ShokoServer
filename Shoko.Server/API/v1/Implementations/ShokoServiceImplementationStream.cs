using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.Rest.Module;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Models.Interfaces;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server
{
    public class ShokoServiceImplementationStream : IShokoServerStream
    {
        //89% Should be enough to not touch matroska offsets and give us some margin
        private double WatchedThreshold = 0.89;

        public const string ServerVersion = "Shoko Stream Server 1.0";
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Stream StreamVideo(int videolocalid, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveVideoLocal(videolocalid, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
            {
                return new StreamWithResponse(r.Status, r.StatusDescription);
            }
            return StreamFromIFile(r, autowatch);
        }

        public Stream StreamVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveFilename(base64filename, userId, autowatch);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
            {
                return new StreamWithResponse(r.Status, r.StatusDescription);
            }
            return StreamFromIFile(r, autowatch);
        }

        private Stream StreamFromIFile(InfoResult r, bool? autowatch)
        {
            try
            {
                Nancy.Request request = RestModule.CurrentModule.Request;

                FileSystemResult<Stream> fr = r.File.OpenRead();
                if (fr == null || fr.Status != Status.Ok)
                {
                    return new StreamWithResponse(HttpStatusCode.InternalServerError,
                        "Unable to open file '" + r.File.FullName + "': " + fr?.Error);
                }
                Stream org = fr.Result;
                long totalsize = org.Length;
                long start = 0;
                long end = totalsize - 1;

                string rangevalue = request.Headers["Range"].FirstOrDefault() ??
                                    request.Headers["range"].FirstOrDefault();
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
                var outstream = new SubStream(org, start, end - start + 1);
                var resp = new StreamWithResponse {ContentType = r.Mime};
                resp.Headers.Add("Server", ServerVersion);
                resp.Headers.Add("Connection", "keep-alive");
                resp.Headers.Add("Accept-Ranges", "bytes");
                resp.Headers.Add("Content-Range", "bytes " + start + "-" + end + "/" + totalsize);
                resp.ContentLength = end - start + 1;

                resp.ResponseStatus = range ? HttpStatusCode.PartialContent : HttpStatusCode.OK;

                if (r.User != null && autowatch.HasValue && autowatch.Value && r.VideoLocal != null)
                {
                    outstream.CrossPosition = (long) (totalsize * WatchedThreshold);
                    outstream.CrossPositionCrossed +=
                        (a) =>
                        {
                            Task.Factory.StartNew(() => { r.VideoLocal.ToggleWatchedStatus(true, r.User.JMMUserID); },
                                new CancellationToken(),
                                TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        };
                }
                resp.Stream = outstream;
                return resp;
            }
            catch (Exception e)
            {
                logger.Error("An error occurred while serving a file: " + e);
                var resp = new StreamWithResponse();
                resp.ResponseStatus = HttpStatusCode.InternalServerError;
                resp.ResponseDescription = e.Message;
                return resp;
            }
        }

        public Stream InfoVideo(int videolocalid, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveVideoLocal(videolocalid, userId, autowatch);
            StreamWithResponse s = new StreamWithResponse(r.Status, r.StatusDescription);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
                return s;
            s.Headers.Add("Server", ServerVersion);
            s.Headers.Add("Accept-Ranges", "bytes");
            s.Headers.Add("Content-Range", "bytes 0-" + (r.File.Size - 1) + "/" + r.File.Size);
            s.ContentType = r.Mime;
            s.ContentLength = r.File.Size;
            return s;
        }

        public Stream InfoVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveFilename(base64filename, userId, autowatch);
            StreamWithResponse s = new StreamWithResponse(r.Status, r.StatusDescription);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
                return s;
            s.Headers.Add("Server", ServerVersion);
            s.Headers.Add("Accept-Ranges", "bytes");
            s.Headers.Add("Content-Range", "bytes 0-" + (r.File.Size - 1) + "/" + r.File.Size);
            s.ContentType = r.Mime;
            s.ContentLength = r.File.Size;
            return s;
        }

        class InfoResult
        {
            public IFile File { get; set; }
            public SVR_VideoLocal VideoLocal { get; set; }
            public SVR_JMMUser User { get; set; }
            public HttpStatusCode Status { get; set; }
            public string StatusDescription { get; set; }
            public string Mime { get; set; }
        }

        private InfoResult ResolveVideoLocal(int videolocalid, int? userId, bool? autowatch)
        {
            try
            {
                InfoResult r = new InfoResult();
                SVR_VideoLocal loc = Repo.VideoLocal.GetByID(videolocalid);
                if (loc == null)
                {
                    r.Status = HttpStatusCode.NotFound;
                    r.StatusDescription = "Video Not Found";
                    return r;
                }
                r.VideoLocal = loc;
                r.File = loc.GetBestFileLink();
                return FinishResolve(r, userId, autowatch);
            }
            catch (Exception e)
            {
                logger.Error("An error occurred while serving a file: " + e);
                var resp = new InfoResult();
                resp.Status = HttpStatusCode.InternalServerError;
                resp.StatusDescription = e.Message;
                return resp;
            }
        }

        public static string Base64DecodeUrl(string base64EncodedData)
        {
            var base64EncodedBytes =
                System.Convert.FromBase64String(base64EncodedData.Replace("-", "+")
                    .Replace("_", "/")
                    .Replace(",", "="));
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
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
                r.User = Repo.JMMUser.GetByID(userId.Value);
                if (r.User == null)
                {
                    r.Status = HttpStatusCode.NotFound;
                    r.StatusDescription = "User Not Found";
                    return r;
                }
            }
            r.Mime = r.File.ContentType;
            if (string.IsNullOrEmpty(r.Mime) || r.Mime.Equals("application/octet-stream",
                    StringComparison.InvariantCultureIgnoreCase))
                r.Mime = MimeTypes.GetMimeType(r.File.FullName);
            r.Status = HttpStatusCode.OK;
            return r;
        }

        private InfoResult ResolveFilename(string filenamebase64, int? userId, bool? autowatch)
        {
            InfoResult r = new InfoResult();
            string fullname = Base64DecodeUrl(filenamebase64);
            r.VideoLocal = null;
            r.File = SVR_VideoLocal.ResolveFile(fullname);
            return FinishResolve(r, userId, autowatch);
        }
    }
}