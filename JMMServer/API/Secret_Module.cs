using Nancy;
using Nancy.Security;

namespace JMMServer.API
{
    public class Secret_Module : NancyModule
    {
        public Secret_Module() : base("/secure")
        {
            this.RequiresAuthentication();
            Get["/"] = x =>
                {
                    //Context.CurrentUser was set by StatelessAuthentication earlier in the pipeline
                    var identity = this.Context.CurrentUser;
                    //return the secure information in a json response
                    var userModel = new JMMServer.Entities.JMMUser(identity.UserName);
                    return this.Response.AsJson(new
                    {
                        SecureContent = "here's some secure content that you can only see if you provide a correct apiKey",
                        User = userModel

                    });

                };
        }
    }
}
