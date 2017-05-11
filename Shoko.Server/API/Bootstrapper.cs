using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nancy.Rest.Module;
using Shoko.Server.API.v2.Modules;
using Shoko.Server.PlexAndKodi.Kodi;
using Shoko.Server.PlexAndKodi;

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
    using core;

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
                    //try to take "apikey" from header
                    string apiKey = nancyContext.Request.Headers["apikey"].FirstOrDefault();
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        //take out value of "apikey" from query that was pass in request and check for User
                        apiKey = (string) nancyContext.Request.Query.apikey.Value;
                    }
                    if (apiKey != null)
                    {
                        return UserDatabase.GetUserFromApiKey(apiKey);
                    }
                    else
                    {
                        return null;
                    }
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

            GzipCompressionSettings gzipsettings = new GzipCompressionSettings();
            gzipsettings.MinimumBytes = 16384; //16k
            gzipsettings.MimeTypes.Add("application/xml");
            gzipsettings.MimeTypes.Add("application/json");
            pipelines.EnableGzipCompression(gzipsettings);

            #endregion
        }

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
            return null;
        }

        private Response BeforeProcessing(NancyContext ctx)
        {
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
            return statusCode == HttpStatusCode.NotFound;
        }

        public void Handle(HttpStatusCode statusCode, NancyContext context)
        {
            try
            {
                if (context.ResolvedRoute.Description.Path.StartsWith("/webui/"))
                {
                    context.Response.Contents = stream =>
                    {
                        var filename = Path.Combine(_rootPathProvider.GetRootPath(), Path.Combine("webui", "index.html"));
                        using (var file = File.OpenRead(filename))
                        {
                            file.CopyTo(stream);
                        }
                    };
                }
                else if (statusCode == HttpStatusCode.NotFound)
                {
                    context.Response = @"<html><body>File not Found (404)</body></html>";
                }
            }
            catch
            {
                context.Response = @"<html><body>Internal Error: #$%^&*(</body></html>";
            }
        }
    }
}