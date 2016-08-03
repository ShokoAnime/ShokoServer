namespace JMMServer.API
{
    using Nancy;
    using Nancy.Authentication.Stateless;
    using Nancy.Bootstrapper;
    using Nancy.TinyIoc;
    using System.Linq;
    using System.Collections.Generic;

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
                    var apiKey = nancyContext.Request.Headers["apikey"].FirstOrDefault();
                    if (apiKey == "")
                    {
                        //take out value of "apikey" from query that was pass in request and check for User
                        apiKey = (string)nancyContext.Request.Query.apikey.Value;
                    }
                    return UserDatabase.GetUserFromApiKey(apiKey);
                });
            StatelessAuthentication.Enable(pipelines, configuration);
        }
    }
}
