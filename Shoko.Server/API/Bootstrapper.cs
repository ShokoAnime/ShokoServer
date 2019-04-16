#if false
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Nancy.Rest.Module;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

namespace Shoko.Server.API
{
    using Nancy;
    //using Nancy.Authentication.Stateless;
    using Nancy.Bootstrapper;
    using Nancy.Conventions;
    using Nancy.TinyIoc;
    using System.Linq;
    using Nancy.ErrorHandling;

    using Nancy.Diagnostics;
    using NLog;
    using System;
    //using Nancy.Gzip;

    public class Bootstrapper : RestBootstrapper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        ///  A list of open connections to the API
        /// </summary>
        private static HashSet<string> _openConnections = new HashSet<string>();
        /// <summary>
        /// blur the connection state to 5s, as most media players and calls are spread.
        /// This prevents flickering of the state for UI
        /// </summary>
        private static Timer _connectionTimer = new Timer(5000);

        static Bootstrapper()
        {
            _connectionTimer.Elapsed += TimerElapsed;
        }

        protected override Func<ITypeCatalog, NancyInternalConfiguration> InternalConfiguration
        {
            //RestBootstraper with use a custom json.net serializer,no need to readd something in here
            get
            {
                return cat =>
                {
                    NancyInternalConfiguration nac = base.InternalConfiguration(cat);
                    nac.ResponseProcessors.Remove(typeof(BinaryProcessor));
                    nac.ResponseProcessors.Insert(0, typeof(BinaryProcessor));
                    return nac;
                };
            }
        }

        private static void AddConnection(NancyContext ctx)
        {
            Guid guid = Guid.NewGuid();
            string id = guid.ToString();
            ctx.Items["ContextGUID"] = id;
            lock (_openConnections)
            {
                _openConnections.Add(id);
                ServerState.Instance.ApiInUse = _openConnections.Count > 0;
            }
        }
        
        private static void RemoveConnection(NancyContext ctx)
        {
            if (!ctx.Items.ContainsKey("ContextGUID")) return;
            lock (_openConnections)
            {
                _openConnections.Remove((string) ctx.Items["ContextGUID"]);
            }
            ResetTimer();
        }

        private static void ResetTimer()
        {
            lock (_connectionTimer)
            {
                _connectionTimer.Stop();
                _connectionTimer.Start();
            }
        }

        private static void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (_openConnections)
            {
                ServerState.Instance.ApiInUse = _openConnections.Count > 0;
            }
        }

        protected override NancyInternalConfiguration InternalConfiguration
        {
            //RestBootstrapper with use a custom json.net serializer,no need to readd something in here
            get
            {
                NancyInternalConfiguration nac = base.InternalConfiguration;
                nac.ResponseProcessors.Remove(typeof(BinaryProcessor));
                nac.ResponseProcessors.Insert(0, typeof(BinaryProcessor));
                return nac;
            }
        }

        private IList<string> MimeTypes { get; set; } = new List<string>
        {
            "text/plain",
            "text/html",
            "text/xml",
            "text/css",
            "application/json",
            "application/x-javascript",
            "application/atom+xml",
            "application/xml"
        };

        public SVR_JMMUser GetRequestUser(NancyContext ctx)
        {
            if (!(ServerState.Instance?.ServerOnline ?? false)) return null;
            string apikey = ctx.Request.Headers["apikey"].FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(apikey))
            {
                // try from query string instead
                try
                {
                    apikey = (string)ctx.Request.Query.apikey.Value;
                }
                catch
                {
                    // ignore
                }
            }
            AuthTokens auth = Repo.Instance.AuthTokens.GetByToken(apikey);
            return auth != null ? Repo.Instance.JMMUser.GetByID(auth.UserID) : null;
        }

        /// <inheritdoc />
        /// <summary>
        /// This function override the RequestStartup which is used each time a request came to Nancy
        /// </summary>
        protected override void RequestStartup(TinyIoCContainer requestContainer, IPipelines pipelines,
            NancyContext context)
        {
            context.CurrentUser = new ClaimsPrincipal(GetRequestUser(context));

            pipelines.OnError += (ctx, ex) => onError(ctx, ex);

            pipelines.BeforeRequest += BeforeProcessing;
            pipelines.AfterRequest += AfterProcessing;

            #region CORS Enable

            pipelines.AfterRequest.AddItemToEndOfPipeline((ctx) =>
            {
                ctx.Response.WithHeader("Access-Control-Allow-Origin", "*")
                    .WithHeader("Access-Control-Allow-Methods", "POST,GET,OPTIONS")
                    .WithHeader("Access-Control-Allow-Headers", "Accept, Origin, Content-type, apikey");
            });

            #endregion

            #region Gzip compression
            pipelines.AfterRequest.AddItemToEndOfPipeline(ctx =>
            {
                if (ctx.Request.Headers.AcceptEncoding.Any(x => x.Contains("gzip"))) return;
                if (ctx.Response.StatusCode != HttpStatusCode.OK) return;
                if (MimeTypes.Any(x => x == ctx.Response.ContentType || ctx.Response.ContentType.StartsWith($"{x};"))) return;
                if (!ctx.Response.Headers.TryGetValue("Content-Length", out var contentLength) || long.Parse(contentLength) < 16384) return;

                ctx.Response.Headers["Content-Encoding"] = "gzip";
                var contents = ctx.Response.Contents;
                ctx.Response.Contents = responseStream =>
                {
                    using (var compression = new GZipStream(responseStream, CompressionMode.Compress))
                    {
                        contents(compression);
                    }
                };
            });
            #endregion

        }

        /// <inheritdoc />
        /// <summary>
        /// overwrite the folder of static content
        /// </summary>
        /// <param name="nancyConventions"></param>
        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            nancyConventions.StaticContentsConventions.Add(
                StaticContentConventionBuilder.AddDirectory("webui", @"webui"));
            base.ConfigureConventions(nancyConventions);
        }

        /*
        protected override DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration {Password = @"jmmserver"}; }
        }
        */

        private Response onError(NancyContext ctx, Exception ex)
        {
            logger.Error("Nancy Error => {0}", ex.ToString());
            logger.Error("Nancy Error => Request URL: {0}", ctx.Request.Url);
            logger.Error(ex);
            var resp = new Response();
            resp.StatusCode = HttpStatusCode.InternalServerError;
            resp.ReasonPhrase = ex.Message;
            return resp;
        }

        private Response BeforeProcessing(NancyContext ctx)
        {
            if (!(ServerState.Instance?.ServerOnline ?? false) && ctx.Request.Path.StartsWith("/api") &&
                !ctx.Request.Path.StartsWith("/api/init/"))
            {
                return new Response
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    ReasonPhrase = "Server is not running"
                };
            }

            if (!ctx.Request.Path.StartsWith("/webui") && !ctx.Request.Path.StartsWith("/api/init/"))
            {
                AddConnection(ctx);
            }

            return null;
        }

        private void AfterProcessing(NancyContext ctx)
        {
            f (!ctx.Request.Path.StartsWith("/webui") && !ctx.Request.Path.StartsWith("/api/init/"))
            {
                RemoveConnection(ctx);
            }
            if (ctx.Request.Method.Equals("OPTIONS", StringComparison.Ordinal))
            {
                Dictionary<string, string> headers = HttpExtensions.GetOptions();
                List<Tuple<string, string>> tps = headers.Select(a => new Tuple<string, string>(a.Key, a.Value))
                    .ToList();
                ctx.Response.WithHeaders(tps.ToArray());
                ctx.Response.ContentType = "text/plain";
            }
        }
    }

    public class StatusCodeHandler : IStatusCodeHandler
    {
        private readonly IRootPathProvider _rootPathProvider;

        public StatusCodeHandler(IRootPathProvider rootPathProvider)
        {
            _rootPathProvider = rootPathProvider;
        }

        public bool HandlesStatusCode(HttpStatusCode statusCode, NancyContext context)
        {
            // If == is true, then 'Handle' will be triggered
            return statusCode == HttpStatusCode.NotFound && (context?.ResolvedRoute?.Description?.Path?.StartsWith("/webui/") ?? false);
        }

        public void Handle(HttpStatusCode statusCode, NancyContext context)
        {
            context.Response.Contents = stream =>
            {
                try
                {
                    var filename = Path.Combine(_rootPathProvider.GetRootPath(), @"webui", "index.html");
                    using (var file = File.OpenRead(filename))
                    {
                        file.CopyTo(stream);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        StreamWriter writer = new StreamWriter(stream, Encoding.Unicode, 128, true);
                        writer.Write(@"<html><body>File not Found (404)</body></html>");
                        writer.Flush();
                        writer.Close();
                    }
                    catch
                    { }
                }
            };
        }
    }
}
#endif