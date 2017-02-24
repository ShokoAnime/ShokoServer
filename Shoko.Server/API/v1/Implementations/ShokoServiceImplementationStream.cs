using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.Rest.Module;
using Shoko.Models;
using Shoko.Models.Server;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Models.Interfaces;
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
                return new StreamWithResponse(r.Status,r.StatusDescription);
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
            return StreamFromIFile(r,autowatch);
        }

        private Stream StreamFromIFile(InfoResult r, bool? autowatch)
        {
            Nancy.Request request = RestModule.CurrentModule.Request;

            FileSystemResult<Stream> fr = r.File.OpenRead();
            if (fr == null || !fr.IsOk)
            {
                return new StreamWithResponse(HttpStatusCode.InternalServerError,"Unable to open file '"+r.File.FullName+"': "+fr?.Error);
            }
            Stream org = fr.Result;

            string rangevalue = request.Headers["Range"].FirstOrDefault() ?? request.Headers["range"].FirstOrDefault();
            rangevalue = rangevalue?.Replace("bytes=", string.Empty);

            long totalsize = org.Length;
            long start = 0;
            long end = totalsize - 1;
            if (!string.IsNullOrEmpty(rangevalue))
            {
                string[] split = rangevalue.Split('-');
                // range: bytes=split[0]-split[1]
                if (split.Length == 2)
                {
                    // bytes=-split[1]
                    if (string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                    {
                        long e = long.Parse(split[1]);
                        start = totalsize - e;
                        end = totalsize - 1;
                    }
                    // bytes=split[0]-
                    else if (!string.IsNullOrEmpty(split[0]) && string.IsNullOrEmpty(split[1]))
                    {
                        start = long.Parse(split[0]);
                        end = totalsize - 1;
                        if (start > end) start = end;
                    }
                    // bytes=split[0]-split[1]
                    else if (!string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                    {
                        start = long.Parse(split[0]);
                        end = long.Parse(split[1]);
                        if (start > totalsize - 1)
                            start = totalsize - 1;
                        if (end > totalsize - 1)
                            end = totalsize - 1;
                        if (start > end) start = end;
                    }
                }
            }
            StreamWithResponse resp = new StreamWithResponse {ContentType = r.Mime};
            resp.Headers.Add("Server", ServerVersion);
            resp.Headers.Add("Connection", "keep-alive");
            resp.Headers.Add("Accept-Ranges", "bytes");
            resp.ResponseStatus = HttpStatusCode.PartialContent;
            resp.Headers.Add("Content-Range", "bytes " + start + "-" + end + "/" + totalsize);
            resp.ContentLength=end - start + 1;
            SubStream outstream = new SubStream(org, start, end - start + 1);

            if (r.User!=null && autowatch.HasValue && autowatch.Value && r.VideoLocal!=null)
            {
                outstream.CrossPosition = (long)((double)totalsize * WatchedThreshold);
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
        public Stream InfoVideo(int videolocalid, int? userId, bool? autowatch, string fakename)
        {
            InfoResult r = ResolveVideoLocal(videolocalid, userId, autowatch);
            StreamWithResponse s = new StreamWithResponse(r.Status, r.StatusDescription);
            if (r.Status != HttpStatusCode.OK && r.Status != HttpStatusCode.PartialContent)
                return s;
            s.Headers.Add("Server", ServerVersion);
            s.Headers.Add("Accept-Ranges", "bytes");
            s.Headers.Add("Content-Range", "bytes " + 0 + "-" + (r.File.Size - 1) + "/" + r.File.Size);
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
            s.Headers.Add("Accept-Ranges","bytes");
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
        private static string GetMime(string fullname)
        {
            string ext = Path.GetExtension(fullname).Replace(".", string.Empty).ToLower();
            switch (ext)
            {
                case "png":
                    return "image/png";
                case "jpg":
                    return "image/jpeg";
                case "mkv":
                    return "video/x-matroska";
                case "mka":
                    return "audio/x-matroska";
                case "mk3d":
                    return "video/x-matroska-3d";
                case "avi":
                    return "video/avi";
                case "mp4":
                    return "video/mp4";
                case "mov":
                    return "video/quicktime";
                case "ogm":
                case "ogv":
                    return "video/ogg";
                case "mpg":
                case "mpeg":
                    return "video/mpeg";
                case "flv":
                    return "video/x-flv";
                case "rm":
                    return "application/vnd.rn-realmedia";
            }
            if (SubtitleHelper.Extensions.ContainsKey(ext))
                return SubtitleHelper.Extensions[ext];
            return "application/octet-stream";
        }
        private InfoResult ResolveVideoLocal(int videolocalid, int? userId, bool? autowatch)
        {
            InfoResult r = new InfoResult();
            SVR_VideoLocal loc = RepoFactory.VideoLocal.GetByID(videolocalid);
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
        public static string Base64DecodeUrl(string base64EncodedData)
        {
            var base64EncodedBytes =
                System.Convert.FromBase64String(base64EncodedData.Replace("-", "+").Replace("_", "/").Replace(",", "="));
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
            if (userId.HasValue && autowatch.HasValue && userId.Value!=0)
            {
                r.User = RepoFactory.JMMUser.GetByID(userId.Value);
                if (r.User == null)
                {
                    r.Status = HttpStatusCode.NotFound;
                    r.StatusDescription = "User Not Found";
                    return r;
                }
            }
            r.Mime = r.File.ContentType;
            if (string.IsNullOrEmpty(r.Mime) || r.Mime.Equals("application/octet-stream", StringComparison.InvariantCultureIgnoreCase))
                r.Mime = GetMime(r.File.FullName);
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