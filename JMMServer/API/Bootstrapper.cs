using Nancy;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using Nancy.Owin;
using System.Linq;
using System.Security.Claims;

namespace JMMServer.API
{
    public class Bootstrapper : DefaultNancyBootstrapper
    {
        protected virtual Nancy.Bootstrapper.NancyInternalConfiguration InternalConfiguration
        {
            //overwrite bootsrapper to use different json implementation
            get { return Nancy.Bootstrapper.NancyInternalConfiguration.WithOverrides(c => c.Serializers.Insert(0, typeof(Nancy.Serialization.JsonNet.JsonNetSerializer))); }
        }

        protected virtual void RequestStartup(TinyIoCContainer container, IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup(container, pipelines, context);
            var owinEnvironment = context.GetOwinEnvironment();
            var user = owinEnvironment["server.User"] as ClaimsPrincipal;
            if (user != null)
            {
                context.CurrentUser = new JMMServer.Entities.JMMUser()
                {
                    UserName = user.Identity.Name,
                    Claims = user.Claims.Where(x => x.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Select(x => x.Value)
                };
            }
        }
    }
}
