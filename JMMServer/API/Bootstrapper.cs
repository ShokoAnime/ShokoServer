namespace JMMServer.API
{
	using Nancy;
	using Nancy.Authentication.Stateless;
	using Nancy.Bootstrapper;
	using Nancy.Conventions;
	using Nancy.TinyIoc;
	using System.Linq;
	using Nancy.Extensions;
	using Nancy.ViewEngines;
	using Nancy.ErrorHandling;
	using Pri.LongPath;
	using Nancy.Diagnostics;

	public class Bootstrapper : DefaultNancyBootstrapper
    {
        protected virtual Nancy.Bootstrapper.NancyInternalConfiguration InternalConfiguration
        {
            //overwrite bootsrapper to use different json implementation
            get { return Nancy.Bootstrapper.NancyInternalConfiguration.WithOverrides(c => c.Serializers.Insert(0, typeof(Nancy.Serialization.JsonNet.JsonNetSerializer))); }
        }

        /// <summary>
        /// This function override the RequestStartup which is used each time a request came to Nancy
        /// </summary>
        protected override void RequestStartup(TinyIoCContainer requestContainer, IPipelines pipelines, NancyContext context)
        {
            var configuration =
                new StatelessAuthenticationConfiguration(nancyContext =>
                {
                    //try to take "apikey" from header
                    string apiKey = nancyContext.Request.Headers["apikey"].FirstOrDefault();
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        //take out value of "apikey" from query that was pass in request and check for User
                        apiKey = (string)nancyContext.Request.Query.apikey.Value;
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
        }

        /// <summary>
        /// overwrite the folder of static content
        /// </summary>
        /// <param name="nancyConventions"></param>
        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            nancyConventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("webui", @"webui"));
            base.ConfigureConventions(nancyConventions);
        }

		protected override DiagnosticsConfiguration DiagnosticsConfiguration
		{
			get { return new DiagnosticsConfiguration { Password = @"jmmserver" }; }
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
                        var filename = Path.Combine(_rootPathProvider.GetRootPath(), @"webui\\index.html");
                        using (var file = File.OpenRead(filename))
                        {
                            file.CopyTo(stream);
                        }
                    };
                }
                else
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
