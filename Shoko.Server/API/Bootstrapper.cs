using System.Collections.Generic;
using System.IO;
using System.Text;
using Nancy.Rest.Module;
using Shoko.Models.Server;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

namespace Shoko.Server.API
{
    using Nancy;
    using Nancy.Authentication.Stateless;
    using Nancy.Bootstrapper;
    using Nancy.Conventions;
    using Nancy.TinyIoc;
    using System.Linq;
    using Nancy.ErrorHandling;
    using Pri.LongPath;
    using Nancy.Diagnostics;
    using NLog;
    using System;
    using Nancy.Gzip;

    public class Bootstrapper : RestBootstrapper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        protected override NancyInternalConfiguration InternalConfiguration
        {
            //RestBootstraper with use a custom json.net serializer,no need to readd something in here
            get
            {
                NancyInternalConfiguration nac = base.InternalConfiguration;
                nac.ResponseProcessors.Remove(typeof(BinaryProcessor));
                nac.ResponseProcessors.Insert(0, typeof(BinaryProcessor));
                return nac;
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// This function override the RequestStartup which is used each time a request came to Nancy
        /// </summary>
        protected override void RequestStartup(TinyIoCContainer requestContainer, IPipelines pipelines,
            NancyContext context)
        {
            StaticConfiguration.EnableRequestTracing = true;
            var configuration =
                new StatelessAuthenticationConfiguration(nancyContext =>
                {
                    // If the server isn't up yet, we can't access the db for users
                    if (!(ServerState.Instance?.ServerOnline ?? false)) return null;
                    // get apikey from header
                    string apiKey = nancyContext.Request.Headers["apikey"].FirstOrDefault()?.Trim();
                    // if not in header
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        // try from query string instead
                        try
                        {
                            apiKey = (string) nancyContext.Request.Query.apikey.Value;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                    AuthTokens auth = RepoFactory.AuthTokens.GetByToken(apiKey);
                    return auth != null
                        ? RepoFactory.JMMUser.GetByID(auth.UserID)
                        : null;
                });
            StaticConfiguration.DisableErrorTraces = false;
            StatelessAuthentication.Enable(pipelines, configuration);

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

            GzipCompressionSettings gzipsettings = new GzipCompressionSettings
            {
                MinimumBytes = 16384 //16k
            };
            gzipsettings.MimeTypes.Add("application/xml");
            gzipsettings.MimeTypes.Add("application/json");
            pipelines.EnableGzipCompression(gzipsettings);

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

        protected override DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration {Password = @"jmmserver"}; }
        }

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
            if (!(ServerState.Instance?.ServerOnline ?? false) && !ctx.Request.Path.StartsWith("/api/init/"))
            {
                return new Response
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    ReasonPhrase = "Server is not running"
                };
            }
            return null;
        }

        private void AfterProcessing(NancyContext ctx)
        {
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
                    {}
                }
            };
        }
    }
}